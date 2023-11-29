using Claire;

class TestUserInterface : IUserInterface
{
    public bool DebugOutput { get; set; }

    public void NextLine()
    {
        // Nothing to do...
    }

    public string Prompt(string message)
    {
        return string.Empty;
    }

    public bool PromptConfirm(string message)
    {
        return true;
    }

    public void Reset()
    {
        // Nothing to do...
    }

    public void SaveFile(string fileName, string contents)
    {
        // Nothing to do...
    }

    public void WriteChatResponse(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteCommand(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteCommandError(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteCommandOutput(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteCompletion(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteDebug(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteError(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteInput(string message, bool newLine = true)
    {
        // Nothing to do...
    }

    public void WriteSystem(string message, bool newLine = true)
    {
        // Nothing to do...
    }
}