namespace Claire;

public enum MessageType
{
    System = 1,
    User,
    Claire, // Assistant
}

public class Message
{
    public MessageType Type { get; set; }
    public string Text { get; set; } = String.Empty;
    public string? FileName { get; set; }

    public static string MessageTypeToString(MessageType type)
    {
        return type switch
        {
            MessageType.System => "system",
            MessageType.User => "user",
            MessageType.Claire => "assistant",
            _ => "Unknown",
        };
    }

}