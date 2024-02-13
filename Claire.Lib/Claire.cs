using System.Linq;
using Azure;
using Azure.AI.OpenAI;

namespace Claire;

public class Claire
{
    private class CommandDefinition(string name, string description, Action function)
    {
        public readonly string Name = name;
        public readonly string Description = description;
        public readonly Action Execute = function;
    }

    private readonly IUserInterface _userInterface;

    private readonly List<CommandDefinition> _commands = [];

    private readonly ClaireConfiguration _configuration;
    private readonly OpenAIClient _openAiClient;

    // Prompt completion state
    private string _promptCompletion = string.Empty;
    private CancellationTokenSource _promptCompletionCancellationTokenSource = new();

    private readonly ClaireShell _shell;

    private readonly IList<Message> _messages = new List<Message>();

    private readonly ChatRequestSystemMessage _promptStartMessage;
    private readonly ChatRequestUserMessage _completionStartMessage;

    private bool _active = false;

    public ClaireConfiguration Configuration => _configuration;
    public IUserInterface UserInterface => _userInterface;

    public Claire(ClaireConfiguration configuration, IUserInterface userInterface)
    {
        _configuration = configuration;
        _userInterface = userInterface;

        _userInterface.DebugOutput = _configuration.Debug;

        // Initialize commands
        _commands.Add(new CommandDefinition("help", "Display a list of commands", CommandHelp));
        _commands.Add(new CommandDefinition("debug", "Enable/disable debug output", CommandDebug));
        _commands.Add(new CommandDefinition("exit", "Exit Claire", CommandExit));

        // Create OpenAI client
        _openAiClient = new OpenAIClient(
            new Uri(_configuration.OpenAiUrl),
            new AzureKeyCredential(_configuration.OpenAiKey)
        );

        // Create shell
        _shell = new ClaireShell(_configuration.ShellProcessName);

        // Create starter prompt
        var intro = $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {_configuration.ShellProcessName} shell.\n";

        var starterPrompt = intro;
        starterPrompt += "You will provide command, scripts, configuration files and explanation to the user\n";
        starterPrompt += "You will also provide help with using the Azure CLI, generate ARM and Bicep templates and help with Github actions.\n";
        _promptStartMessage = new ChatRequestSystemMessage(starterPrompt);

        // Create completion prompt
        var completionPrompt = intro;
        completionPrompt += "Users will provide the start of a question related to the shell or command and you will complete the question or command for them. Do not answer the question. Just provide a completion. Make sure to include a space at the start of the completion if necessary.";
        _completionStartMessage = new ChatRequestUserMessage(completionPrompt);
    }

    private void AddMessage(MessageType messageType, string text)
    {
        var message = new Message
        {
            Type = messageType,
            Text = text,
        };

        _messages.Add(message);
    }

    private List<ChatRequestMessage> GetConversationHistory(int size)
    {
        var messages = _messages
            .Select<Message, ChatRequestMessage>(m => m.Type switch 
            {
                MessageType.User => new ChatRequestUserMessage(m.Text),
                MessageType.Claire => new ChatRequestAssistantMessage(m.Text),
                _ => throw new Exception("Internal error: Invalid message type"),
            })
            .TakeLast(_configuration.ChatHistorySize)
            .ToList();

        return messages;
    }

    private List<ChatRequestMessage> PrepareChatMessages(string prompt, ChatRequestMessage startMessage)
    {
        var messages = GetConversationHistory(_configuration.ChatHistorySize);

        messages.Insert(0, startMessage);

        messages.Add(new ChatRequestUserMessage(prompt));

        return messages;
    }

    private async Task GetPromptCompletionsAsync(string prompt)
    {
        if (!_configuration.SuggestCompletions)
        {
            // Suggestions are turned off.
            return;
        }

        if (prompt.Length < 3)
        {
            // Wait until we have at least 3 characters
            return;
        }

        if (prompt[0] == '/')
        {
            // Don't complete commands...yet?
            return;
        }

        if (_promptCompletionCancellationTokenSource != null)
        {
            _promptCompletionCancellationTokenSource.Cancel();
            _promptCompletionCancellationTokenSource.Dispose();
        }

        _promptCompletionCancellationTokenSource = new CancellationTokenSource();

        _promptCompletion = string.Empty;

        // Wait before requesting completions
        await Task.Delay(_configuration.SuggestionDelay, _promptCompletionCancellationTokenSource.Token);

        var requestPrompt = prompt;

        _promptCompletion = await ExecuteCompletion(
            requestPrompt,
            _promptCompletionCancellationTokenSource.Token
        );

        _userInterface.WriteCompletion(_promptCompletion, newLine: false);
    }

    private void ErasePromptCompletion()
    {
        if (_promptCompletion.Length > 0)
        {
            var backSpaces = new string('\b', _promptCompletion.Length);
            var spaces = new string(' ', _promptCompletion.Length);

            Console.Write(backSpaces);
            Console.Write(spaces);
            Console.Write(backSpaces);
        }
    }

    private string GetUserPrompt()
    {
        string prompt = string.Empty;

        do
        {
            _userInterface.WriteSystem("Please tell me what you would like to do?", newLine: false);
            if (_configuration.SuggestCompletions)
            {
                _userInterface.WriteSystem(" (Press TAB for suggestions)", newLine: false);
            }
            _userInterface.NextLine();

            char keyChar;

            do
            {
                var key = Console.ReadKey(true);
                keyChar = key.KeyChar;

                ErasePromptCompletion();

                switch (keyChar)
                {
                    case '\t':
                        if (_promptCompletion.Length > 0)
                        {
                            prompt += _promptCompletion;
                            _userInterface.WriteInput(_promptCompletion, newLine: false);

                            // We purposely don't `await` this call to allow user to continue typing
                            _ = GetPromptCompletionsAsync(prompt);
                        }
                        break;

                    case '\r':
                    case '\n':
                        // Ignore CR/LF. They are checked for at the end of the loop.
                        break;

                    case '\b':
                        if (prompt.Length > 0)
                        {
                            if (Console.CursorLeft == 0)
                            {
                                // On a new line. Need to re-draw the prompt.
                                _userInterface.WriteInput(prompt, newLine: false);
                            }
                            else
                            {
                                // Erase the last character
                                prompt = prompt.Substring(0, prompt.Length - 1);
                                Console.Write("\b \b"); // Erase the current character with a space, and back again
                            }
                        }
                        break;

                    default:
                        prompt += keyChar;
                        _userInterface.WriteInput(keyChar.ToString(), newLine: false);

                        // We purposely don't `await` this call to allow user to continue typing
                        _ = GetPromptCompletionsAsync(prompt);

                        break;
                }
            } while (keyChar != '\r' && keyChar != '\n');

            // Move to the next line.
            _userInterface.NextLine();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _userInterface.WriteSystem("Use `/help` to see a list of commands.");
            }
        } while (string.IsNullOrWhiteSpace(prompt));

        return prompt;
    }

    private string ConvertMessageTypeToRole(MessageType type)
    {
        return type switch
        {
            MessageType.User => "user",
            MessageType.Claire => "system",
            _ => throw new Exception("Internal error: Invalid message type"),
        };
    }

    private async Task<string> ExecuteChatPrompt(string prompt, bool saveHistory = false)
    {
        return await ExecuteChat(
            prompt,
            _promptStartMessage,
            temperature: 0.7f,
            maxTokens: 800,
            saveHistory: saveHistory
        );
    }

    private async Task<string> ExecuteCompletion(string prompt, CancellationToken cancellationToken)
    {
        return await ExecuteChat(
            prompt,
            _completionStartMessage,
            temperature: 0.0f,
            maxTokens: 20,
            cancellationToken: cancellationToken,
            saveHistory: false
        );
    }

    private async Task<string> ExecuteChat(string prompt, ChatRequestMessage startMessage, float temperature = 0.7f, int maxTokens = 800, CancellationToken cancellationToken = new CancellationToken(), bool saveHistory = false)
    {
        var messages = PrepareChatMessages(prompt, startMessage);

        var options = new ChatCompletionsOptions(_configuration.OpenAiModel, messages);
        options.Temperature = temperature;

        _userInterface.WriteDebug($"prompt: {prompt}");

        var response = await _openAiClient.GetChatCompletionsAsync(options, cancellationToken);

        var responseMessage = response.Value.Choices[0].Message.Content;

        _userInterface.WriteDebug($"response: {responseMessage}");

        if (saveHistory)
        {
            _messages.Add(new Message() { Type = MessageType.User, Text = prompt });
            _messages.Add(new Message() { Type = MessageType.Claire, Text = responseMessage });
        }

        return responseMessage;
    }

    private async Task<ChatResponse> GetIntentAsync(string prompt, ChatResponse intent)
    {
        var intentPrompt =
            $"Determine if the following statement is asking about a specific command, generate a file, or an explanation:\n\n";
        intentPrompt += $"\"{prompt}\"\n\n";
        intentPrompt += $"Reply only with the word `command`, `file`, `explain` or 'unknown.";

        var intentText = await ExecuteChatPrompt(intentPrompt);

        switch (intentText.ToLower())
        {
            case "command":
                intent.Type = ChatResponseType.Command;
                break;

            case "file":
                intent.Type = ChatResponseType.File;
                break;

            case "explain":
                intent.Type = ChatResponseType.Explain;
                break;

            case "unknown":
                intent.Type = ChatResponseType.Unknown;
                break;

            default:
                Console.Error.WriteLine($"Unknown intent type: {intentText}");
                intent.Type = ChatResponseType.Unknown;
                break;
        }

        return intent;
    }

    private async Task<ChatResponse> GetUnknownAsync(string prompt, ChatResponse intent)
    {
        var response = await ExecuteChatPrompt(prompt, saveHistory: true);

        intent.Response = response;

        return intent;
    }

    private async Task<ChatResponse> GetCommandAsync(string prompt, ChatResponse intent)
    {
        var commandPrompt = $"Provide the command required for the statement below:\n\n";
        commandPrompt += $"{prompt}\n\n";
        commandPrompt += $"Reply with only the text for the command. Do not include explanation or markdown.";

        var commandText = await ExecuteChatPrompt(commandPrompt, saveHistory: true);
        
        commandText = RemoveMarkdownFences(commandText);

        intent.Response = commandText;

        return intent;
    }

    private async Task<ChatResponse> GetFileNameAsync(string prompt, ChatResponse intent)
    {
        var fileNamePrompt = $"Provide the file name associated with this prompt:\n\n";
        fileNamePrompt += $"{prompt}\n\n";
        fileNamePrompt +=
            $"Provide only the file name in the response. No additional text. If no file name are found in the prompt, then propose an appropriate file name.";

        var fileName = await ExecuteChatPrompt(fileNamePrompt);

        fileName = RemoveMarkdownFences(fileName);

        intent.FileName = fileName;

        return intent;
    }

    private string RemoveMarkdownFences(string text)
    {
        if (text.StartsWith("```"))
        {
            //Strip off the markdown
            var lines = text.Split('\n');

            text = string.Join("\n", lines.Skip(1).SkipLast(1));
        }

        return text;
    }

    private async Task<ChatResponse> GetFileAsync(string prompt, ChatResponse intent)
    {
        var filePrompt = $"Generate the file requested below:\n\n";
        filePrompt += $"{prompt}\n\n";
        filePrompt += $"Respond with only the content of the file without any Markdown annotation. No additional text before or after the content of the file. Comments in the file are permissible";

        var responseText = await ExecuteChatPrompt(filePrompt, saveHistory: true);

        responseText = RemoveMarkdownFences(responseText);

        intent.Response = responseText;

        return intent;
    }

    private async Task<ChatResponse> GetExplanationAsync(string prompt, ChatResponse intent)
    {
        var response = await ExecuteChatPrompt(prompt, saveHistory: true);

        intent.Response = response;

        return intent;
    }


    public async Task<ChatResponse> GetPromptResult(string prompt)
    {
        var intent = new ChatResponse();

        await GetIntentAsync(prompt, intent);

        switch (intent.Type)
        {
            case ChatResponseType.Unknown:
                await GetUnknownAsync(prompt, intent);
                break;

            case ChatResponseType.Command:
                await GetCommandAsync(prompt, intent);
                break;

            case ChatResponseType.File:
                await GetFileNameAsync(prompt, intent);
                await GetFileAsync(prompt, intent);
                break;

            case ChatResponseType.Explain:
                await GetExplanationAsync(prompt, intent);
                break;

            default:
                throw new Exception("Internal error: Unknown intent type");
        }

        _messages.Add(new Message()
        {
            Type = MessageType.User,
            Text = intent.Response,
            FileName = intent.FileName,
        });

        return intent;
    }

    private async Task<ShellResult> ExecuteCommand(string command)
    {
        try
        {
            _userInterface.WriteDebug($"command: {command}");

            var result = await _shell.Execute(command);

            _userInterface.WriteDebug($"stdout: {result.Output}");
            _userInterface.WriteDebug($"stderr: {result.Error}");

            return result;
        }
        catch (Exception exception)
        {
            _userInterface.WriteError($"Exception: {exception.Message}");

            throw;
        }
    }

    private async Task ExecuteIntentUnknown(ChatResponse intent)
    {
        _userInterface.WriteSystem("Sorry, I don't understand what you mean. Please try again.");
    }

    private async Task<string> GetErrorDescription(string command, string error)
    {
        var prompt = $"Explain why the command `{command}` encountered the following error:\n";
        prompt += $"{error}\n";

        var response = await ExecuteChatPrompt(prompt, saveHistory: true);

        return response;
    }

    private async Task ExecuteIntentCommand(ChatResponse intent)
    {
        _userInterface.WriteSystem($"I believe the command you are looking for is:");
        _userInterface.WriteChatResponse($"{intent.Response}");
        _userInterface.NextLine();

        var execute = _userInterface.PromptConfirm("Shall I executed it for you?");

        if (execute)
        {
            var result = await ExecuteCommand(intent.Response);

            _userInterface.WriteCommand(result.Output);

            if (result.HasError)
            {
                _userInterface.WriteCommandError(result.Error);

                _userInterface.WriteSystem("It looks like the command encountered a problem. Investigating...");

                var errorDescription = await GetErrorDescription(intent.Response, result.Error);

                _userInterface.WriteChatResponse(errorDescription);
            }
        }
    }

    private async Task ExecuteIntentFile(ChatResponse action)
    {
        _userInterface.WriteSystem($"The following file was generated:");
        _userInterface.WriteChatResponse(action.Response);

        var saveFile = _userInterface.PromptConfirm($"Would you like to save the file '{action.FileName}'?");

        if (saveFile)
        {

            if (string.IsNullOrEmpty(action.FileName))
            {
                var fileName = _userInterface.Prompt("Please enter a filename: ");

                if (string.IsNullOrEmpty(fileName))
                {
                    _userInterface.WriteSystem("No file name provide. File will not be saved.");
                    return;
                }

                action.FileName = fileName;
            }

            try
            {
                // TODO: Need to save through working directory of the command line...
                await File.WriteAllTextAsync(action.FileName, action.Response);
                _userInterface.WriteSystem($"File {action.FileName} saved.");
            }
            catch (Exception exception)
            {
                _userInterface.WriteCommandError($"Could not save file {action.FileName}: {exception.Message}");
            }
        }
    }

    private async Task ExecuteIntentExplain(ChatResponse action)
    {
        _userInterface.WriteChatResponse(action.Response);
    }

    private async Task ExecuteIntent(ChatResponse action)
    {
        switch (action.Type)
        {
            case ChatResponseType.Unknown:
                await ExecuteIntentUnknown(action);
                break;

            case ChatResponseType.Command:
                await ExecuteIntentCommand(action);
                break;

            case ChatResponseType.File:
                await ExecuteIntentFile(action);
                break;

            case ChatResponseType.Explain:
                await ExecuteIntentExplain(action);
                break;

            default:
                throw new Exception($"Unexpected response: {action.Type}");
        }
    }

    public void Stop()
    {
        _active = false;
    }

    public async Task PromptUser() 
    {
            var prompt = GetUserPrompt();

            if (!string.IsNullOrEmpty(prompt) && prompt[0] == '/')
            {
                var command = prompt.Substring(1);

                var commandDefinition = _commands.FirstOrDefault(c => c.Name == command);

                if (commandDefinition == null)
                {
                    _userInterface.WriteSystem($"Unknown command: {command}");
                    _userInterface.WriteSystem($"Use `/help` to see a list of commands.");
                }
                else
                {
                    commandDefinition.Execute();
                }
            }
            else
            {
                _userInterface.WriteSystem("Let me think about that for a moment...");

                var intent = await GetPromptResult(prompt);

                _userInterface.WriteDebug($"Intent: {intent.Type}");
                _userInterface.WriteDebug($"Command: {intent.Response}");
                _userInterface.WriteDebug($"File Name: {intent.FileName}");

                await ExecuteIntent(intent);
            }
    }

    public async Task Run()
    {
        _userInterface.WriteSystem("Welcome to Claire. Where would you like to go today?");

        _active = true;

        while (_active)
        {
            await PromptUser();
        }

        _userInterface.Reset();
    }

    private void CommandHelp()
    {
        UserInterface.WriteSystem("Available commands:");

        foreach (var command in _commands)
        {
            UserInterface.WriteSystem($"  /{command.Name} - {command.Description}");
        }
    }

    private void CommandExit()
    {
        Stop();
    }

    private void CommandDebug()
    {
        if (_userInterface.DebugOutput)
        {
            _userInterface.DebugOutput = false;
            _userInterface.WriteSystem("Debug output now is off");
        }
        else
        {
            _userInterface.DebugOutput = true;
            _userInterface.WriteSystem("Debug output now is on");
        }
    }
}