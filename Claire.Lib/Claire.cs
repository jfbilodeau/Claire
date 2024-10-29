namespace Claire;

public class Claire
{
    public IUserInterface UserInterface { get; private set; }
    public ClaireShell Shell { get; private set; }
    public bool Debug
    {
        get => UserInterface.DebugOutput;
        set => UserInterface.DebugOutput = value;
    }
    private readonly ClaireKernel _kernel;
    private bool _active;

    public Claire(ClaireConfiguration configuration, IUserInterface userInterface)
    {
        UserInterface = userInterface;

        UserInterface.DebugOutput = configuration.Debug;

        // Create Claire kernel
        _kernel = new ClaireKernel(configuration);

        // Create shell
        Shell = new ClaireShell(configuration.ShellProcessName);
    }

    private string GetUserPrompt()
    {
        while (true)
        {
            var prompt = UserInterface.Prompt("Please tell me what you would like to do?");

            // Move to the next line.
            UserInterface.NextLine();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                UserInterface.WriteSystem(
                    "Sorry, I didn't hear what you said. Feel free to ask for help at any time.");
            }
            else
            {
                return prompt;
            }
        }
    }

    public async Task ExecuteCommandPromptUser(string command)
    {
        UserInterface.WriteSystem($"I believe the command you are looking for is:");
        UserInterface.WriteChatResponse($"{command}");
        UserInterface.NextLine();

        var execute = UserInterface.PromptConfirm("Shall I executed it for you?");

        if (execute)
        {
            var result = await ExecuteCommand(command);

            UserInterface.WriteCommand(result.Output);

            if (result.HasError)
            {
                UserInterface.WriteCommandError(result.Error);

                UserInterface.WriteSystem("It looks like the command encountered a problem. Investigating...");

                var errorDescription = await GetErrorDescription(command, result.Error);

                UserInterface.WriteChatResponse(errorDescription);
            }
        }
    }

    private async Task<ShellResult> ExecuteCommand(string command)
    {
        try
        {
            UserInterface.WriteDebug($"command: {command}");

            var result = await Shell.Execute(command);

            UserInterface.WriteDebug($"stdout: {result.Output}");
            UserInterface.WriteDebug($"stderr: {result.Error}");

            return result;
        }
        catch (Exception exception)
        {
            UserInterface.WriteError($"Exception: {exception.Message}");

            throw;
        }
    }

    public async Task<string> GetErrorDescription(string command, string error)
    {
        var prompt = $"Explain why the command `{command}` encountered the following error:\n";
        prompt += $"{error}\n";

        // When requesting an explanation, do not execute tools
        var response = await _kernel.GetChatMessageContentsAsync(prompt, useTools: false);

        return response[0].Content;
    }

    private async Task<ClaireAction> ExecutePrompt(string prompt, bool useTools)
    {
        do
        {
            try
            {
                var result = await _kernel.ExecutePrompt(prompt, useTools: useTools);

                return result;            
            }
            catch (Exception exception)
            {
                UserInterface.WriteError("Claire encountered the following error:");
                UserInterface.WriteError(exception.Message);

                var tryAgain = UserInterface.PromptConfirm("Try again?");

                if (tryAgain)
                {
                    // Skip return and try prompt again.
                    continue;
                }

                // Exit Claire
                Stop();
            }

        } while (true);
    }

    public async Task GenerateFile(string fileName, string content, string description)
    {
        UserInterface.WriteSystem($"I've generated the following:");
        UserInterface.WriteChatResponse(content);
        UserInterface.WriteSystem(description);

        var saveFile = UserInterface.PromptConfirm($"Would you like to save the file '{fileName}'?");
        if (saveFile)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = UserInterface.Prompt("Please enter a filename: ");

                if (string.IsNullOrEmpty(fileName))
                {
                    UserInterface.WriteSystem("No file name provide. File will not be saved.");
                    return;
                }
            }

            try
            {
                // TODO: Need to save through working directory of the command line...
                UserInterface.SaveFile(fileName, content);
                UserInterface.WriteSystem($"File {fileName} saved.");
            }
            catch (Exception exception)
            {
                UserInterface.WriteCommandError($"Could not save file {fileName}: {exception.Message}");
            }
        }
    }

    public void Stop()
    {
        _active = false;
    }

    public void SetDebug(bool debug)
    {
        UserInterface.DebugOutput = debug;
    }

    public async Task PromptUser()
    {
        var prompt = GetUserPrompt();

        UserInterface.WriteSystem("Let me think about that for a moment...");

        UserInterface.WriteDebug($"executing prompt: {prompt}");

        var result = await ExecutePrompt(prompt, useTools: true);

        UserInterface.WriteDebug($"response: {result}");

        await ProcessPromptResult(result);
    }

    private async Task ProcessPromptResult(ClaireAction action)
    {
        try
        {
            await action.Execute(this);
        }
        catch (Exception exception)
        {
            UserInterface.WriteError($"An unexpected error has occurred: {exception.Message}");
            UserInterface.WriteDebug(exception.StackTrace ?? "No stack trace available.");
            UserInterface.WriteSystem("This is a bug in Claire. Please report this issue.");
        }
    }

    public async Task Run()
    {
        UserInterface.WriteWarning("Claire is a proof of concept and uses AI. You are in control but it can make mistakes. Execute commands at your own risk.");
        UserInterface.WriteSystem("Welcome to Claire. Where would you like to go today?");

        _active = true;

        while (_active)
        {
            await PromptUser();
        }

        UserInterface.Reset();
    }
}