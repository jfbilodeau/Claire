namespace Claire;

public interface IUserInterface
{
    bool DebugOutput { get; set; }

    void Reset();

    string Prompt(string message);

    bool PromptConfirm(string message);

    void NextLine();

    void WriteDebug(string message, bool newLine = true);

    void WriteError(string message, bool newLine = true);

    void WriteSystem(string message, bool newLine = true);

    void WriteInput(string message, bool newLine = true);

    void WriteChatResponse(string message, bool newLine = true);

    void WriteCommand(string message, bool newLine = true);

    void WriteCompletion(string message, bool newLine = true);

    void WriteCommandOutput(string message, bool newLine = true);

    void WriteCommandError(string message, bool newLine = true);

    void SaveFile(string fileName, string contents);
}