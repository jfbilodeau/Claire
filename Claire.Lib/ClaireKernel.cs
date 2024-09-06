using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;

namespace Claire;

public enum PromptResultAction
{
    DisplayChatMessage,
    ExecuteCommand,
    GenerateFile,
    Quit,
}

public class PromptResult
{
    private readonly string _result;
    private readonly string _fileName;

    private PromptResult(PromptResultAction action, string result = "", string fileName = "")
    {
        Action = action;
        _result = result;
        _fileName = fileName;
    }

    public static PromptResult DisplayChatMessage(string message)
    {
        return new PromptResult(PromptResultAction.DisplayChatMessage, message);
    }

    public static PromptResult ExecuteCommand(string command)
    {
        return new PromptResult(PromptResultAction.ExecuteCommand, command);
    }

    public static PromptResult GenerateFile(string fileName, string content)
    {
        return new PromptResult(PromptResultAction.GenerateFile, content, fileName);
    }

    public static PromptResult Quit()
    {
        return new PromptResult(PromptResultAction.Quit);
    }

    public PromptResultAction Action { get; }

    public string Message => Action == PromptResultAction.DisplayChatMessage
        ? _result
        : throw new InvalidOperationException("Result is not a chat message.");

    public string Command => Action == PromptResultAction.ExecuteCommand
        ? _result
        : throw new InvalidOperationException("Result is not a command.");

    public string FileName => Action == PromptResultAction.GenerateFile
        ? _fileName
        : throw new InvalidOperationException("Result is not a file.");

    public string Content => Action == PromptResultAction.GenerateFile
        ? _result
        : throw new InvalidOperationException("Result is not a file.");

    public override string ToString()
    {
        return $"{Action}: {_result} ({_fileName})";
    }
}

public class ClaireKernel
{
    private PromptResult? _promptResult = null;

    private readonly Kernel _kernel;

    private readonly ChatHistory _chatHistory = [];

    public ClaireKernel(ClaireConfiguration configuration)
    {
        // Create system prompt
        var systemPrompt = $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {configuration.ShellProcessName} shell.\n";
        systemPrompt += "You will provide command, scripts, configuration files and explanation to the user\n";
        systemPrompt += "You will also provide help with using the Azure CLI, generate ARM and Bicep templates and help with Github actions.\n";
        systemPrompt += "Execute the function 'execute_command' to execute a shell or CLI command. Prompt the user for missing parameters.\n";
        systemPrompt += "Execute the function 'generate_script' when you need to generate code, script or a file for the user. Explain the script but do not repeat the script to the user.\n";
        _chatHistory.AddSystemMessage(systemPrompt);

        // Create kernel
        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.AddAzureOpenAIChatCompletion(
            configuration.OpenAiModel,
            configuration.OpenAiUrl,
            configuration.OpenAiKey
        );
#pragma warning disable SKEXP0050
        kernelBuilder.Plugins.AddFromType<TimePlugin>();
#pragma warning restore SKEXP0050

        kernelBuilder.Plugins.AddFromObject(this);

        _kernel = kernelBuilder.Build();
    }

    public async Task<PromptResult> ExecutePrompt(string prompt, bool useTools)
    {
        _promptResult = null;
        
        _chatHistory.AddUserMessage(prompt);

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
        
        // Create chat completion service options
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResultsPerPrompt = 1,
            // ToolCallBehavior = useTools ? ToolCallBehavior.AutoInvokeKernelFunctions : ToolCallBehavior.EnableFunctions([], false)
        };

        IReadOnlyList<ChatMessageContent> response;
        
        if (useTools)
        {
            executionSettings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
            response = await chatCompletionService.GetChatMessageContentsAsync(
                _chatHistory,
                executionSettings,
                _kernel
            );
        }
        else
        {
            response = await chatCompletionService.GetChatMessageContentsAsync(
                _chatHistory,
                executionSettings
            );
        }

        _promptResult ??= PromptResult.DisplayChatMessage(response[0].Content ?? string.Empty);

        return _promptResult;
    }
    
    
    [KernelFunction()]
    [Description("Execute a command for the shell configured for Claire. Can also be used to execute Azure CLI commands. Should not be called when asking to explain errors.")]
    public void RunCommand(string command)
    {
        _promptResult = PromptResult.ExecuteCommand(command);
    }

    [KernelFunction]
    [Description("Generate a file, template or script based on the prompt.")]
    private void GenerateFile(string fileName, string content)
    {
        _promptResult = PromptResult.GenerateFile(fileName, content);
    }

    [KernelFunction]
    [Description("Exit Claire")]
    public void ExitClaire()
    {
        _promptResult = PromptResult.Quit();
    }
}