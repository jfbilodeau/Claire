using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

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

    // private readonly AzureOpenAIClient _openAiClient;
    private readonly OpenAIPromptExecutionSettings _openAiPromptExecutionSettings;

    private readonly Kernel _kernel;
    
    private readonly ClaireShell _shell;

    private readonly ChatHistory _chatHistory = new();

    private bool _active;
    private bool _suppressAssistantOutput;

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


        // Create system prompt
        var systemPrompt = $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {_configuration.ShellProcessName} shell.\n";
        systemPrompt += "You will provide command, scripts, configuration files and explanation to the user\n";
        systemPrompt += "You will also provide help with using the Azure CLI, generate ARM and Bicep templates and help with Github actions.\n";
        systemPrompt += "Execute the function 'execute_command' to execute a shell or CLI command. Prompt the user for missing parameters.\n";
        systemPrompt += "Execute the function 'generate_script' when you need to generate code, script or a file for the user. Explain the script but do not repeat the script to the user.\n";
        _chatHistory.AddSystemMessage(systemPrompt);
        
        // Create chat completion service options
        _openAiPromptExecutionSettings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // Create kernel
        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                _configuration.OpenAiModel,
                _configuration.OpenAiUrl,
                _configuration.OpenAiKey
            );

        kernelBuilder.Plugins.AddFromObject(this);

        _kernel = kernelBuilder.Build();

        // Create shell
        _shell = new ClaireShell(_configuration.ShellProcessName);
    }

    private string GetUserPrompt()
    {
        while (true)
        {
            _userInterface.WriteSystem("Please tell me what you would like to do?");

            var prompt = Console.ReadLine();

            // Move to the next line.
            _userInterface.NextLine();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _userInterface.WriteSystem("Use `/help` to see a list of commands.");
            }
            else
            {
                return prompt;
            }
        }
    }

    private async Task<string> ExecuteChatPrompt(string prompt)
    {
        return await ExecuteChat(
            prompt
        );
    }

    private async Task<string> ExecuteChat(string prompt)
    {
        _chatHistory.AddUserMessage(prompt);

        _userInterface.WriteDebug($"prompt: {prompt}");

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        var response = await chatCompletionService.GetChatMessageContentsAsync(
            _chatHistory,
            _openAiPromptExecutionSettings,
            _kernel
        );

        var responseMessage = response[0].Content ?? string.Empty;

        _userInterface.WriteDebug($"response: {responseMessage}");
        // _userInterface.WriteDebug($"Tokens sent: {response.Value.Usage.PromptTokens}, Tokens received: {response.Value.Usage.CompletionTokens}, Total: {response.Value.Usage.TotalTokens}");

        return responseMessage;
    }

    private async Task<ShellResult> ExecuteCommand(string command)
    {
        _suppressAssistantOutput = true;

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

    private async Task<string> GetErrorDescription(string command, string error)
    {
        var prompt = $"Explain why the command `{command}` encountered the following error:\n";
        prompt += $"{error}\n";

        var response = await ExecuteChatPrompt(prompt);

        return response;
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
            var command = prompt[1..];

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

            _suppressAssistantOutput = false;

            var response = await ExecuteChat(prompt);

            if (!_suppressAssistantOutput)
            {
                _userInterface.WriteChatResponse(response);
            }
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

    [KernelFunction("execute_command")]
    [Description("Execute a command for the shell configured for Claire. Can also be used to execute Azure CLI commands.")]
    public async Task RunCommand(
        string command
    )
    {
        _userInterface.WriteSystem($"I believe the command you are looking for is:");
        _userInterface.WriteChatResponse($"{command}");
        _userInterface.NextLine();

        var execute = _userInterface.PromptConfirm("Shall I executed it for you?");

        if (execute)
        {
            var result = await ExecuteCommand(command);

            _userInterface.WriteCommand(result.Output);

            if (result.HasError)
            {
                _userInterface.WriteCommandError(result.Error);

                _userInterface.WriteSystem("It looks like the command encountered a problem. Investigating...");

                var errorDescription = await GetErrorDescription(command, result.Error);

                _userInterface.WriteChatResponse(errorDescription);
            }
        }
    }

    [KernelFunction("generate_script")]
    [Description("Generate a file, template or script based on the prompt.")]
    private async Task GenerateFile(string fileName, string content)
    {
        _suppressAssistantOutput = false;

        _userInterface.WriteSystem($"The following file was generated:");
        _userInterface.WriteChatResponse(content);

        var saveFile = _userInterface.PromptConfirm($"Would you like to save the file '{fileName}'?");

        if (saveFile)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = _userInterface.Prompt("Please enter a filename: ");

                if (string.IsNullOrEmpty(fileName))
                {
                    _userInterface.WriteSystem("No file name provide. File will not be saved.");
                    return;
                }
            }

            try
            {
                // TODO: Need to save through working directory of the command line...
                await File.WriteAllTextAsync(fileName, content);
                _userInterface.WriteSystem($"File {fileName} saved.");
            }
            catch (Exception exception)
            {
                _userInterface.WriteCommandError($"Could not save file {fileName}: {exception.Message}");
            }
        }
        else
        {
            // Do not display the explanation of the script
            _suppressAssistantOutput = true;
        }
    }
    
    [KernelFunction("exit_claire")]
    [Description("Exit Claire.")]
    public void ExitClaire()
    {
        var response = _userInterface.PromptConfirm("Are you sure you want to exit Claire?");
        
        if (response)
        {
            Stop();
        }
    }
}