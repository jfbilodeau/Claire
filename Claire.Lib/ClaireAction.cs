namespace Claire;

public abstract class ClaireAction
{
    public abstract Task Execute(Claire claire);
}

public class ChatResponseAction : ClaireAction {
    private readonly string _message;

    public ChatResponseAction(string message) {
        _message = message;
    }

    public override Task Execute(Claire claire) {
        claire.UserInterface.WriteChatResponse(_message);

        return Task.CompletedTask;
    }
}

public class ExecuteCommandAction : ClaireAction {
    private readonly string _command;

    public ExecuteCommandAction(string command) {
        _command = command;
    }

    public override async Task Execute(Claire claire) {
        await claire.ExecuteCommandPromptUser(_command);
    }
}


public class GenerateFileAction : ClaireAction {
    private readonly string _fileName;
    private readonly string _content;
    private readonly string _description;

    public GenerateFileAction(string fileName, string content, string description) {
        _fileName = fileName;
        _content = content;
        _description = description;
    }

    public override async Task Execute(Claire claire) {
        claire.UserInterface.WriteDebug($"Generating file: {_fileName}");

        await claire.GenerateFile(_fileName, _content, _description);

        claire.UserInterface.WriteChatResponse(_description);
    }
}

public class EnableDebugAction : ClaireAction {
    public override Task Execute(Claire claire) {
        claire.UserInterface.WriteDebug("Debug enabled");

        claire.Debug = true;

        return Task.CompletedTask;
    }
}

public class DisableDebugAction : ClaireAction {
    public override Task Execute(Claire claire) {
        claire.UserInterface.WriteDebug("Debug disabled");

        claire.Debug = false;

        return Task.CompletedTask;
    }
}

public class ToggleDebugAction : ClaireAction {
    public override Task Execute(Claire claire) {
        claire.Debug = !claire.Debug;

        claire.UserInterface.WriteDebug($"Debug toggled: {claire.Debug}");

        return Task.CompletedTask;
    }
}

public class QuitAction : ClaireAction {
    public override Task Execute(Claire claire) {
        claire.Stop();

        return Task.CompletedTask;
    }
}
