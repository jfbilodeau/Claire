namespace Claire;

public class Claire
{
    private class CommandDefinition(string name, string description, Action function)
    {
        public readonly string Name = name;
        public readonly string Description = description;
        public readonly Action Execute = function;
    }

    private readonly List<CommandDefinition> _commands = [];

    private readonly IUserInterface _userInterface;

    private readonly ClaireShell _shell;

    private readonly ClaireKernel _kernel;

    private bool _active;

    public Claire(ClaireConfiguration configuration, IUserInterface userInterface)
    {
        _userInterface = userInterface;

        _userInterface.DebugOutput = configuration.Debug;

        // Initialize commands
        _commands.Add(new CommandDefinition("help", "Display a list of commands", CommandHelp));
        _commands.Add(new CommandDefinition("debug", "Enable/disable debug output", CommandDebug));
        _commands.Add(new CommandDefinition("exit", "Exit Claire", CommandExit));

        // Create Claire kernel
        _kernel = new ClaireKernel(configuration);

        // Create shell
        _shell = new ClaireShell(configuration.ShellProcessName);
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

    private async Task ExecuteCommandPromptUser(string command)
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

    private async Task<string> GetErrorDescription(string command, string error)
    {
        var prompt = $"Explain why the command `{command}` encountered the following error:\n";
        prompt += $"{error}\n";

        // When requesting an explanation, do not execute tools
        var result = await _kernel.ExecutePrompt(prompt, useTools: false);

        return result.Message;
    }

    public async Task GenerateFile(string fileName, string content)
    {
        _userInterface.WriteSystem($"I've generated the following:");
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

            _userInterface.WriteDebug($"executing prompt: {prompt}");

            var result = await _kernel.ExecutePrompt(prompt, useTools: true);

            _userInterface.WriteDebug($"response: {result}");
            // _userInterface.WriteDebug($"Tokens sent: {response.Value.Usage.PromptTokens}, Tokens received: {response.Value.Usage.CompletionTokens}, Total: {response.Value.Usage.TotalTokens}");

            switch (result.Action)
            {
                case PromptResultAction.DisplayChatMessage:
                    _userInterface.WriteChatResponse(result.Message);
                    break;

                case PromptResultAction.ExecuteCommand:
                    await ExecuteCommandPromptUser(result.Command);
                    break;

                case PromptResultAction.GenerateFile:
                    await GenerateFile(result.FileName, result.Content);
                    break;
                
                case PromptResultAction.Quit:
                    Stop();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
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
        _userInterface.WriteSystem("Available commands:");

        foreach (var command in _commands)
        {
            _userInterface.WriteSystem($"  /{command.Name} - {command.Description}");
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