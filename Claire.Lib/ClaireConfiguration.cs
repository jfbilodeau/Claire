namespace Claire;

public class ClaireConfiguration
{
    public string OpenAiUrl { get; set; } = string.Empty;
    public string OpenAiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = string.Empty;
    public string ShellProcessName { get; set; } = string.Empty;
    public bool Debug { get; set; } = false;
}