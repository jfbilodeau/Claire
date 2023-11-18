using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Azure;
using Azure.AI.OpenAI;

namespace Claire;

public class Claire
{
    private class CommandDefinition
    {
        public CommandDefinition(string name, string description, Action function)
        {
            Name = name;
            Description = description;
            Function = function;
        }

        public readonly string Name;
        public readonly string Description;
        public readonly Action Function;
    }

    private readonly UserInterface _userInterface = new();

    private readonly List<CommandDefinition> _commands = new();

    private readonly ClaireConfiguration _configuration;
    private readonly OpenAIClient _openAiClient;
    private readonly Process _process;
    private readonly StreamWriter _processWriter;
    private readonly StreamReader _processReader;
    private readonly StreamReader _processErrorReader;

    private readonly IList<Message> _messages = new List<Message>();

    private bool _active = false;

    public ClaireConfiguration Configuration => _configuration;
    public UserInterface UserInterface => _userInterface;

    public Claire(ClaireConfiguration configuration)
    {
        _configuration = configuration;

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
        
        // Create backend console.
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _configuration.ShellProcessName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // CreateNoWindow = !_configuration.Debug,
            CreateNoWindow = false,
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };
        var process = Process.Start(processStartInfo);

        _process = process ?? throw new Exception("Failed to start backend console");

        _processWriter = _process.StandardInput;
        _processReader = _process.StandardOutput;
        _processErrorReader = _process.StandardError;
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

    private List<ChatMessage> GetConversationHistory(int size)
    {
        var messages = _messages
            .Where(m => m.Type == MessageType.Claire || m.Type == MessageType.User)
            .Select(m => new ChatMessage(ConvertMessageTypeToRole(m.Type), m.Text))
            .TakeLast(_configuration.ChatHistorySize)
            .ToList();

        return messages;
    }

    private List<ChatMessage> PrepareChatMessages(string prompt)
    {
        var messages = GetConversationHistory(10);
        
        // Always initialize conversation with starter prompt
        var starterPrompt = $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {_configuration.ShellProcessName} shell.\n";
        starterPrompt += "You will provide command, scripts, configuration files and explanation to the user\n";
        starterPrompt += "You will also provide help with using the Azure CLI.\n";
        var starterMessage = new ChatMessage("system", starterPrompt);
        
        messages.Insert(0, starterMessage);
        
        messages.Add(new ChatMessage(ConvertMessageTypeToRole(MessageType.User), prompt));

        return messages;
    }

    private string GetUserPrompt()
    {
        string? prompt;

        do
        {
            _userInterface.WriteSystem("Please tell me what you would like to do?");

            prompt = Console.ReadLine();

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
        var messages = PrepareChatMessages(prompt);

        var options = new ChatCompletionsOptions(_configuration.OpenAiModel, messages);
        
        _userInterface.WriteDebug($"prompt: {prompt}");

        var response = await _openAiClient.GetChatCompletionsAsync(options);

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
        var intentPrompt = $"Determine if the following statement is asking about a specific command, generate a file, or an explanation:\n\n";
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

        intent.Response = commandText;

        return intent;
    }

    private async Task<ChatResponse> GetFileNameAsync(string prompt, ChatResponse intent)
    {
        var fileNamePrompt = $"Provide the file name associated with this prompt:\n\n";
        fileNamePrompt += $"{prompt}\n\n";
        fileNamePrompt += $"Provide only the file name in the response. No additional text. If no file name are found in the prompt, then respond with the following text: <<unknown>>";

        var fileName = await ExecuteChatPrompt(fileNamePrompt);

        if (fileName.Contains("<<unknown>>"))
        {
            fileName = "";
        }

        intent.FileName = fileName;

        return intent;
    }

    private async Task<ChatResponse> GetFileAsync(string prompt, ChatResponse intent)
    {
        var filePrompt = $"Generate the file requested below:\n\n";
        filePrompt += $"{prompt}\n\n";
        filePrompt += $"Respond with only the content of the file without any Markdown annotation. No additional text before or after the content of the file. Comments are permissible";

        var responseText = await ExecuteChatPrompt(filePrompt, saveHistory: true);

        if (responseText.StartsWith("```"))
        {
            //Strip off the markdown
            var lines = responseText.Split('\n');

            responseText = string.Join("\n", lines.Skip(1).SkipLast(1));

        }
        
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
    
    private async Task<string> ReadStream(StreamReader reader)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int byteRead;

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            while ((byteRead = await reader.ReadAsync(buffer, cancellationTokenSource.Token)) > 0)
            {
                builder.Append(buffer, 0, byteRead);
            }
        }
        catch (OperationCanceledException)
        {
            // reader.ReadAsync will throw a TaskCanceledException when the timeout is reached.
        }

        return builder.ToString();
    }

    private async Task<CommandResult> ExecuteCommand(string command)
    {
        try
        {
            await _processWriter.WriteAsync($"{command}{_processWriter.NewLine}");
            await _processWriter.FlushAsync();

            var readDelay = TimeSpan.FromSeconds(1);
            
            await Task.Delay(readDelay);

            var result = new CommandResult
            {
                Output = await ReadStream(_processReader),
                Error = await ReadStream(_processErrorReader),
            };

            return result;
        }
        catch (Exception exception)
        {   
            Console.WriteLine($"Exception: {exception.Message}");
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
        _userInterface.WriteChat($"{intent.Response}");
        _userInterface.WriteLine();

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

                _userInterface.WriteChat(errorDescription);
            }
        }
    }

    private async Task ExecuteIntentFile(ChatResponse action)
    {
        _userInterface.WriteSystem($"The following file was generated:");
        _userInterface.WriteChat(action.Response);

        var saveFile = _userInterface.PromptConfirm("Would you like to save the file?");

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
        _userInterface.WriteChat(action.Response);
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

    public async Task Run()
    {
        Console.WriteLine("Welcome to Claire. Where would you like to go today?");

        _active = true;

        while (_active)
        {
            var prompt = GetUserPrompt();

            if (!string.IsNullOrEmpty(prompt) && prompt[0] == '/')
            {
                var command = prompt.Substring(1);

                var foundCommand = _commands.FirstOrDefault(c => c.Name == command);

                if (foundCommand == null)
                {
                    _userInterface.WriteSystem($"Unknown command: {command}");
                    _userInterface.WriteSystem($"Use `/help` to see a list of commands.");
                }
                else
                {
                    foundCommand.Function();
                }
            }
            else
            {
                var intent = await GetPromptResult(prompt);

                _userInterface.WriteDebug($"Intent: {intent.Type}");
                _userInterface.WriteDebug($"Command: {intent.Response}");
                _userInterface.WriteDebug($"File Name: {intent.FileName}");
                
                await ExecuteIntent(intent);
            }
        }
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