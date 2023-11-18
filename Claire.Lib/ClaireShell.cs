using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Claire;

// Wrapper for a shell process
public class ClaireShell
{
    private readonly string ClaireShellPrompt = "ClaireShell";
    
    private readonly string _shellProcessName;
    private readonly Process _process;
    private readonly StreamWriter _processWriter;
    private readonly StreamReader _processReader;
    private readonly StreamReader _processErrorReader;

    private bool executingCommand = false;

    public ClaireShell(string shellProcessName)
    {
        _shellProcessName = shellProcessName;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = shellProcessName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // CreateNoWindow = !_configuration.Debug,
            CreateNoWindow = false,
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };
        var process = Process.Start(processStartInfo);

        _process = process ?? throw new Exception("Failed to start backend console");

        _processWriter = _process.StandardInput;
        _processReader = _process.StandardOutput;
        _processErrorReader = _process.StandardError;

        // Set the shell prompt to include delimiter.
        // This is used to detect when a command has finished executing.
        if (shellProcessName.Contains("bash"))
        {
            Execute($"export PS1=\"{ClaireShellPrompt}\\n$PS1\"").Wait();
        }
        else if (shellProcessName.Contains("cmd", StringComparison.InvariantCultureIgnoreCase))
        {
            Execute($"prompt {ClaireShellPrompt}$_$P$G").Wait();
        }
        else if (shellProcessName.Contains("powershell", StringComparison.InvariantCultureIgnoreCase))
        {
            Execute($"function prompt {{ \"{ClaireShellPrompt}`n$($executionContext.SessionState.Path.CurrentLocation)$('>' * ($nestedPromptLevel + 1)) \" }}").Wait();
        }
        else
        {
            throw new Exception($"Unknown shell name: {shellProcessName}");
        }
    }

    private async Task<string> ReadStandardOut(StreamReader reader)
    {
        var EndStreamToken = $"{_processWriter.NewLine}{ClaireShellPrompt}{_processWriter.NewLine}";
        
        var response = string.Empty;
        var buffer = new char[4096];
        int byteRead;

        while ((byteRead = await reader.ReadAsync(buffer)) > 0)
        {
            response += new string(buffer, 0, byteRead);

            if (response.Contains(EndStreamToken))
            {
                break;
            }
        }
        
        // Remove the delimiter from the end of the response
        response = response.Replace(EndStreamToken, string.Empty);

        return response;
    }
    
    private async Task<string> ReadStandardError(StreamReader reader)
    {
        var response = string.Empty;
        var buffer = new char[4096];
        int byteRead;

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            while ((byteRead = await reader.ReadAsync(buffer, cancellationTokenSource.Token)) > 0)
            {
                response += new string(buffer, 0, byteRead);
            }
        }
        catch (OperationCanceledException)
        {
            // reader.ReadAsync will throw a TaskCanceledException when the timeout is reached.
        }

        return response;
    }

    public async Task<ShellResult> Execute(string command)
    {
        if (executingCommand)
        {
            throw new Exception("Cannot execute command while another command is executing");
        }

        executingCommand = true;

        // Write command to shell
        await _processWriter.WriteAsync($"{command}{_processWriter.NewLine}");
        await _processWriter.FlushAsync();

        // Read output from shell
        var result = new ShellResult
        {
            Output = await ReadStandardOut(_processReader),
            Error = await ReadStandardError(_processErrorReader),
        };
        
        executingCommand = false;

        return result;
    }
}