using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;


public class Claire
{
    class CommandDefinition
    {
        public CommandDefinition(string name, string description, Action function)
        {
            Name = name;
            Description = description;
            Function = function;
        }

        public string Name;
        public string Description;
        public Action Function;
    }

    private readonly Output _output = new();

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
    public Output Output => _output;

    public Claire(ClaireConfiguration configuration)
    {
        _configuration = configuration;

        // Validate configuration
        if (string.IsNullOrWhiteSpace(_configuration.OpenAiUrl))
        {
            throw new Exception("OpenAiUrl is required");
        }

        if (string.IsNullOrWhiteSpace(_configuration.OpenAiKey))
        {
            throw new Exception("OpenAiKey is required");
        }

        if (string.IsNullOrWhiteSpace(_configuration.OpenAiModel))
        {
            throw new Exception("OpenAIModel is required");
        }

        // Initialize commands
        _commands.Add(new CommandDefinition("help", "Display a list of commands", CommandHelp));
        _commands.Add(new CommandDefinition("exit", "Exit Claire", CommandExit));

        // Create OpenAI client
        _openAiClient = new OpenAIClient(
            new Uri(_configuration.OpenAiUrl),
            new AzureKeyCredential(_configuration.OpenAiKey)
        );

        // Initialize OpenAI with starter prompt
        var starterPrompt = $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {_configuration.Process} shell.\n";
        starterPrompt += "You will respond to user prompts with the appropriate command, file, or explanation.\n";
        // var starterPrompt = $"You will provide precise response to queries about {_configuration.Process} shell.\n";
        // // starterPrompt += "If asked who you are, you will response that you are Claire, a Command-Line AI Runtime Environment who guides users with the {_configuration.Process} shell.\n";
        AddMessage(MessageType.User, starterPrompt);

        // Create backend console.
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _configuration.Process,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = !_configuration.Debug,
        };
        _process = Process.Start(processStartInfo);

        if (_process == null)
        {
            throw new Exception("Failed to start backend console");
        }

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
            .TakeLast(10)
            .ToList();

        return messages;
    }

    private string GetUserPrompt()
    {
        string? prompt;

        do
        {
            _output.WriteSystem("Please tell me what you would like to do?");

            prompt = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _output.WriteSystem("Use `/help` to see a list of commands.");
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

    private async Task<string> ExecuteChatPrompt(string prompt)
    {
        var messages = GetConversationHistory(10);

        messages.Add(new ChatMessage(ConvertMessageTypeToRole(MessageType.User), prompt));

        var options = new ChatCompletionsOptions(_configuration.OpenAiModel, messages);

        var response = await _openAiClient.GetChatCompletionsAsync(options);

        var responseMessage = response.Value.Choices[0].Message.Content.ToLowerInvariant();

        Console.WriteLine($"Response: {responseMessage}");

        return responseMessage;
    }

    private async Task<ChatResponse> GetIntentAsync(string prompt, ChatResponse intent)
    {
        var intentPrompt = $"Determine if the following statement is asking about a shell command, a file, or explanation:\n\n";
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
        var response = await ExecuteChatPrompt(prompt);

        intent.Response = response;

        return intent;
    }

    private async Task<ChatResponse> GetCommandAsync(string prompt, ChatResponse intent)
    {
        var commandPrompt = $"Provide the command required for the statement below:\n\n";
        commandPrompt += $"{prompt}\n\n";
        commandPrompt += $"Reply with only the text for the command. Do not include explanation or markdown.";

        var commandText = await ExecuteChatPrompt(commandPrompt);

        intent.Response = commandText;

        return intent;
    }

    private async Task<ChatResponse> GetFileNameAsync(string prompt, ChatResponse intent)
    {
        var fileNamePrompt = $"Provide the file name associated with this prompt:\n\n";
        fileNamePrompt += $"{prompt}\n\n";
        fileNamePrompt += $"Provide only the file name in the response. No additional text. If no file name are found in the prompt, then respond with the following text: <<unknown>>";

        var fileName = await ExecuteChatPrompt(fileNamePrompt);

        if (fileName.ToLower().Contains("Unknown"))
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
        filePrompt += $"Respond only the content of the file. Do not include explanations or markdown.";

        var responseText = await ExecuteChatPrompt(filePrompt);

        var lines = responseText.Split("\n");

        if (lines.Length < 2)
        {
            intent.Type = ChatResponseType.Unknown;
            intent.Response = responseText;

            return intent;
        }

        intent.Response = lines[0];
        intent.FileName = String.Join("\n", lines.Skip(1));

        return intent;
    }

    private async Task<ChatResponse> GetExplainedAsync(string prompt, ChatResponse intent)
    {
        var response = await ExecuteChatPrompt(prompt);

        intent.Response = response;

        return intent;
    }


    private async Task<ChatResponse> GetPromptResult(string prompt)
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
                await GetExplainedAsync(prompt, intent);
                break;

            default:
                throw new Exception("Internal error: Unknown intent type");
        }

        return intent;
    }

    private async Task<CommandResult> ExecuteCommand(string command)
    {
        _processWriter.Write($"{command}\n");

        await Task.Delay(TimeSpan.FromSeconds(1));

        var result = new CommandResult();

        result.Output = _processReader.ReadToEnd();
        result.Error = _processErrorReader.ReadToEnd();

        return result;
    }

    private async Task ExecuteIntentUnknown(ChatResponse intent)
    {
        _output.WriteSystem($"Sorry, I don't understand what you mean. Please try again.");
    }

    private async Task<string> GetErrorDescription(string command, string error)
    {
        var prompt = $"Explain why the command `{command}` encountered the following error:\n";
        prompt += $"{error}";

        var response = await ExecuteChatPrompt(prompt);

        return response;
    }

    private async Task ExecuteIntentCommand(ChatResponse intent)
    {
        _output.WriteSystem($"I believe the command you are looking for is:");
        _output.WriteChat($"{intent.Response}");
        _output.WriteLine();
        _output.WriteSystem($"Shall I executed it for you? (Y/N): ");

        var input = Console.ReadKey();

        _output.WriteLine();  // Since input does not contain CR, move to the next line.

        if (input.KeyChar == 'y' || input.KeyChar == 'Y')
        {
            var result = await ExecuteCommand(intent.Response);

            _output.WriteCommand(result.Output);

            if (result.HasError)
            {
                _output.WriteCommandError(result.Error);

                _output.WriteSystem("It looks like the command encountered a problem. Investigating...");

                var errorDescription = await GetErrorDescription(intent.Response, result.Error);

                _output.WriteChat(errorDescription);
            }
        }
    }

    private async Task ExecuteIntentFile(ChatResponse action)
    {
        _output.WriteSystem($"The following file was generated:");
        _output.WriteChat(action.Response);

        var saveFile = _output.PromptConfirm("Would you like to save it?");

        if (saveFile)
        {
            if (string.IsNullOrEmpty(action.FileName))
            {
                var fileName = _output.Prompt("Please enter a filename: ");

                if (string.IsNullOrEmpty(fileName))
                {
                    _output.WriteSystem("No file name provide. File will not be saved.");
                    return;
                }
            }

            // TODO: Need to save through working directory of the command line...
            File.WriteAllText(action.FileName, action.Response);
        }
    }

    private async Task ExecuteIntentExplain(ChatResponse action)
    {
        _output.WriteChat(action.Response);
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
                    _output.WriteSystem($"Unknown command: {command}");
                    _output.WriteSystem($"Use `/help` to see a list of commands.");
                }
                else
                {
                    foundCommand.Function();
                }
            }
            else
            {
                var intent = await GetPromptResult(prompt);

                Console.WriteLine($"Intent: {intent.Type}");
                Console.WriteLine($"Command: {intent.Response}");
                Console.WriteLine($"File: {intent.FileName}");

                ExecuteIntent(intent);
            }
        }
    }

    private void CommandHelp()
    {
        Output.WriteSystem("Available commands:");

        foreach (var command in _commands)
        {
            Output.WriteSystem($"  /{command.Name} - {command.Description}");
        }
    }

    private void CommandExit()
    {
        Stop();
    }
}