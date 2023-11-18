namespace Claire;

public class CommandResult 
{
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;

    public bool HasError  => !string.IsNullOrEmpty(Error);
}