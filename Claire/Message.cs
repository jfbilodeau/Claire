namespace Claire;

public enum MessageType
{
    User = 1,
    Claire,
    Command,
    File,
}

public class Message
{
    public MessageType Type { get; set; }
    public string Text { get; set; } = String.Empty;
    public string? FileName { get; set; }
}