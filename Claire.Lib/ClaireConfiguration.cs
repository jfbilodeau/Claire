namespace Claire;

public class ClaireConfiguration
{
    public string OpenAiUrl { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = string.Empty;
    public string ShellProcessName { get; set; } = string.Empty;
    public int ChatHistorySize { get; set; } = 10;
    public bool Debug { get; set; } = false;
}