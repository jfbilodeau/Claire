namespace Claire;

using Azure.AI.OpenAI;

public class Output
{
    public bool DebugOutput { get; set; } = false;
    
    public string Prompt(string message)
    {
        WriteSystem(message);

        var result = Console.ReadLine() ?? string.Empty;

        return result;
    }

    public bool PromptConfirm(string message)
    {
        WriteSystem($"{message} Y/N: ");

        var key = Console.ReadKey();

        WriteLine();

        return key.KeyChar == 'Y' || key.KeyChar == 'y';
    }

    public void WriteLine()
    {
        Console.WriteLine();
    }

    public void WriteDebug(string message, bool newLine = true)
    {
        if (DebugOutput)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(message);

            if (newLine)
            {
                WriteLine();
            }
        }
    }

    public void WriteSystem(string message, bool newLine = true)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(message);

        if (newLine)
        {
            WriteLine();
        }
    }

    public void WriteChat(string message, bool newLine = true)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(message);

        if (newLine)
        {
            WriteLine();
        }
    }

    public void WriteCommand(string message, bool newLine = true)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(message);

        if (newLine)
        {
            WriteLine();
        }
    }

    public void WriteCommandOutput(string message, bool newLine = true)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(message);

        if (newLine) 
        {
            WriteLine();
        }
    }

    public void WriteCommandError(string message, bool newLine = true)
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write(message);

        if (newLine) 
        {
            WriteLine();
        }
    }
}