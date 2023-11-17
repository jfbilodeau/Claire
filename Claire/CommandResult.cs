public class CommandResult 
{
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";

    public bool HasError  => string.IsNullOrEmpty(Error);
}