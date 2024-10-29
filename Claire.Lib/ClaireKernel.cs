using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;

namespace Claire;

// public enum PromptResultAction
// {
//     DisplayChatMessage,
//     ExecuteCommand,
//     GenerateFile,
//     EnableDebug,
//     DisableDebug,
//     ToggleDebug,
//     Quit,
//     Exception,
// }

// public class PromptResult
// {
//     private readonly string _result;
//     private readonly string _fileName;
//     private readonly string _description;

//     private PromptResult(
//         PromptResultAction action,
//         string result = "",
//         string fileName = "",
//         string description = ""
//     )
//     {
//         Action = action;
//         _result = result;
//         _fileName = fileName;
//         _description = description;
//     }

//     public static PromptResult DisplayChatMessage(string message)
//     {
//         return new PromptResult(PromptResultAction.DisplayChatMessage, message);
//     }

//     public static PromptResult ExecuteCommand(string command)
//     {
//         return new PromptResult(PromptResultAction.ExecuteCommand, command);
//     }

//     public static PromptResult GenerateFile(string fileName, string content, string description)
//     {
//         return new PromptResult(PromptResultAction.GenerateFile, content, fileName, description);
//     }

//     public static PromptResult EnableDebug()
//     {
//         return new PromptResult(PromptResultAction.EnableDebug);
//     }

//     public static PromptResult DisableDebug()
//     {
//         return new PromptResult(PromptResultAction.DisableDebug);
//     }

//     public static PromptResult ToggleDebug()
//     {
//         return new PromptResult(PromptResultAction.ToggleDebug);
//     }

//     public static PromptResult Quit()
//     {
//         return new PromptResult(PromptResultAction.Quit);
//     }

//     public static PromptResult Exception(string message)
//     {
//         return new PromptResult(PromptResultAction.Exception, message);
//     }

//     public PromptResultAction Action { get; }

//     public string Message => Action == PromptResultAction.DisplayChatMessage
//         ? _result
//         : throw new InvalidOperationException("Result is not a chat message.");

//     public string Command => Action == PromptResultAction.ExecuteCommand
//         ? _result
//         : throw new InvalidOperationException("Result is not a command.");

//     public string FileName => Action == PromptResultAction.GenerateFile
//         ? _fileName
//         : throw new InvalidOperationException("Result is not a file.");

//     public string Content => Action == PromptResultAction.GenerateFile
//         ? _result
//         : throw new InvalidOperationException("Result is not a file.");
    
//     public string Description => Action == PromptResultAction.GenerateFile
//         ? _description
//         : throw new InvalidOperationException("Result is not a file.");

//     public string ErrorMessage => Action == PromptResultAction.Exception
//         ? _result
//         : throw new InvalidOperationException("Result is not an exception.");

//     public override string ToString()
//     {
//         return $"{Action}: {_result} ({_fileName})";
//     }
// }

public class ClaireKernel
{
    private ClaireAction? _action = null;

    private readonly Kernel _kernel;

    private readonly ChatHistory _chatHistory = [];

    public ClaireKernel(ClaireConfiguration configuration)
    {
        // Create system prompt
        var systemPrompt =
            $"You are Claire, a Command-Line AI Runtime Environment who guides users with the {configuration.ShellProcessName} shell and executes commands on their behalf.\n";
        systemPrompt += "You will provide command, scripts, configuration files and explanation to the user\n";
        systemPrompt +=
            "You will also provide help with using the Azure CLI, generate ARM and Bicep templates and help with Github actions.\n";
        systemPrompt +=
            "Execute the function 'ExecuteCommand' when the user is asking about a shell or CLI command. Prompt the user for missing parameters.\n";
        systemPrompt +=
            "Execute the function 'ExecuteScript' when you need to generate code, script or a file for the user. Explain the script but do not repeat the script to the user.\n";
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

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(string prompt, bool useTools) {
        _chatHistory.AddUserMessage(prompt);

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        // Create chat completion service options
        var executionSettings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        var kernel = _kernel;

        if (!useTools)
        {
            // Do not include the kernel to prevent Semantic Kernel from executing tools
            kernel = null;
        }

        var response = await chatCompletionService.GetChatMessageContentsAsync(
            _chatHistory,
            executionSettings,
            kernel
        );

        return response;
    }

    public async Task<ClaireAction> ExecutePrompt(string prompt, bool useTools)
    {
        _action = null;

        var response = await GetChatMessageContentsAsync(prompt, useTools);

        var responseContent = response[0].Content;

        if (!string.IsNullOrEmpty(responseContent))
        {
            _chatHistory.AddAssistantMessage(responseContent);
        }

        _action ??= new ChatResponseAction(response[0].Content ?? string.Empty);

        return _action;
    }

    [KernelFunction]
    [Description("Execute a command for the shell configured for Claire. Can also be used to execute Azure CLI commands. Prompt the user for missing parameters.")]
    public void RunCommand(string command)
    {
        _action = new ExecuteCommandAction(command);
    }

    [KernelFunction]
    [Description("Generate a file, template or script based on the prompt.")]
    private void GenerateFile(
        [Description("The file name")]
        string fileName, 
        [Description("The content of the file")]
        string content, 
        [Description("A description or explanation of the file, template or script is and how it works. Do not use Markdown.")]
        string description
    )
    {
        _action = new GenerateFileAction(fileName, content, description);
    }

    [KernelFunction]
    [Description("Enable debug mode")]
    public void EnableDebug()
    {
        _action = new EnableDebugAction();
    }

    [KernelFunction]
    [Description("Disable debug mode")]
    public void DisableDebug()
    {
        _action = new DisableDebugAction();
    }

    [KernelFunction]
    [Description("Toggle debug mode on or off")]
    public void ToggleDebug()
    {
        _action = new ToggleDebugAction();
    }

    [KernelFunction]
    [Description("Exit Claire")]
    public void ExitClaire()
    {
        _action = new QuitAction();
    }
}