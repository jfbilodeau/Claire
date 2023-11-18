namespace Claire;

public enum ChatResponseType
{
    Unknown = 1,
    Command,
    File,
    Explain,
}

public class ChatResponse
{
    public ChatResponseType Type { get; set; }
    public string Response { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

}
