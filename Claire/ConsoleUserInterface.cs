namespace Claire;

public class ConsoleUserInterface : IUserInterface
{
    private readonly ConsoleColor _defaultForegroundColor = Console.ForegroundColor;
    private readonly ConsoleColor _defaultBackgroundColor = Console.BackgroundColor;

    public bool DebugOutput { get; set; } = false;

    public void Reset()
    {
        Console.ForegroundColor = _defaultForegroundColor;
        Console.BackgroundColor = _defaultBackgroundColor;
    }

    public string Prompt(string message)
    {
        WriteSystem(message);
        
        Write("> ", ConsoleColor.Cyan, _defaultBackgroundColor, false);
        var result = Console.ReadLine() ?? string.Empty;

        return result;
    }

    public bool PromptConfirm(string message)
    {
        WriteSystem($"{message} Y/N: ", newLine: false);

        var key = Console.ReadLine();

        return key is { Length: > 0 } && (key[0] == 'Y' || key[0] == 'y');
    }

    public void NextLine()
    {
        Console.WriteLine();
    }

    private void Write(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor, bool newLine = true)
    {
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = backgroundColor;

        Console.Write(message);

        if (newLine)
        {
            NextLine();
        }
    }

    public void WriteDebug(string message, bool newLine = true)
    {
        if (DebugOutput)
        {
            Write(message, ConsoleColor.DarkGray, ConsoleColor.Black, newLine);
        }
    }

    public void WriteError(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.Red, ConsoleColor.Black, newLine);
    }

    public void WriteSystem(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.White, ConsoleColor.Black, newLine);
    }

    public void WriteInput(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.Magenta, ConsoleColor.Black, newLine);
    }

    public void WriteChatResponse(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.Gray, ConsoleColor.Black, newLine);
    }

    public void WriteCommand(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.DarkYellow, ConsoleColor.Black, newLine);
    }

    public void WriteCommandOutput(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.Green, ConsoleColor.Black, newLine);
    }

    public void WriteCommandError(string message, bool newLine = true)
    {
        Write(message, ConsoleColor.Red, ConsoleColor.Black, newLine);
    }

    public void SaveFile(string fileName, string contents)
    {
        File.WriteAllText(fileName, contents);
    }

    public void WriteWarning(string message)
    {
        Write(message, ConsoleColor.Yellow, ConsoleColor.Black);
    }
}