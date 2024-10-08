﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Claire;

// Wrapper for a shell process
public class ClaireShell
{
    private const string ClaireShellPrompt = "ClaireShellPromptMarker";

    private readonly string _shellProcessName;
    private readonly string _commandPrefix = string.Empty;
    private readonly string _commandSuffix = string.Empty;
    private Process _process;
    private StreamWriter _processWriter;
    private StreamReader _processReader;
    private StreamReader _processErrorReader;

    private bool _executingCommand = false;

    public ClaireShell(string shellProcessName)
    {
        _shellProcessName = shellProcessName;
        
        if (_shellProcessName.Contains("bash"))
        {
            // Bash does not echo the shell prompt to stdout so need output it manually
            _commandSuffix = $" ; echo {ClaireShellPrompt}";
        }

        CreateShellProcess();
    }

    public void Reset()
    {
        FreeShellProcess();
        CreateShellProcess();
    }

    private void FreeShellProcess()
    {
        _process.Kill();
        _process.Dispose();
        
        _processWriter.Dispose();
        _processReader.Dispose();
        _processErrorReader.Dispose();
    }

    [MemberNotNull(
        nameof(_process), 
        nameof(_processWriter), 
        nameof(_processReader), 
        nameof(_processErrorReader)
    )]
    private void CreateShellProcess()
    {
        var arguments = string.Empty;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _shellProcessName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };

        var process = Process.Start(processStartInfo);

        _process = process ?? throw new Exception("Failed to start backend console");

        _processWriter = _process.StandardInput;
        _processReader = _process.StandardOutput;
        _processErrorReader = _process.StandardError;

        // Set the shell prompt to include delimiter.
        // This is used to detect when a command has finished executing.
        if (_shellProcessName.Contains("bash"))
        {
            // Using _commandSuffix instead to output the `ClairShellPrompt` to stdout
        }
        else if (_shellProcessName.Contains("cmd", StringComparison.InvariantCultureIgnoreCase))
        {
            Execute($"prompt {ClaireShellPrompt}$_$P$G").Wait();
        }
        else if (_shellProcessName.Contains("powershell", StringComparison.InvariantCultureIgnoreCase))
        {
            var prompt =
                $"function prompt {{ \"{ClaireShellPrompt}`r`n$($executionContext.SessionState.Path.CurrentLocation)$('>' * ($nestedPromptLevel + 1)) \" }}";
            
            Execute(prompt).Wait();
        }
        else
        {
            throw new ArgumentException($"Unknown shell name: {_shellProcessName}");
        }
    }

    private async Task<string> ReadUntilPrompt(StreamReader reader)
    {
        var endStreamToken = $"{ClaireShellPrompt}{_processWriter.NewLine}";

        var response = string.Empty;
        var buffer = new char[4096];
        int byteRead;

        while ((byteRead = await reader.ReadAsync(buffer)) > 0)
        {
            response += new string(buffer, 0, byteRead);

            if (response.Contains(endStreamToken))
            {
                break;
            }
        }

        // Remove the delimiter from the end of the response
        response = response.Replace(endStreamToken, string.Empty);

        // Get rid of the last line which is the command prompt
        var lines = response.Split(_processWriter.NewLine);
        response = string.Join(_processWriter.NewLine, lines[..^1]);
        
        return response;
    }

    private async Task<string> ReadUntilEnd(StreamReader reader)
    {
        var response = string.Empty;
        var buffer = new char[4096];

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            int byteRead;
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
        if (_executingCommand)
        {
            throw new Exception("Cannot execute command while another command is executing");
        }

        _executingCommand = true;

        // Write command to shell
        await _processWriter.WriteAsync($"{_commandPrefix}{command}{_commandSuffix}{_processWriter.NewLine}");

        var result = new ShellResult()
        {
            Output = await ReadUntilPrompt(_processReader),
            Error = await ReadUntilEnd(_processErrorReader),
        };

        _executingCommand = false;

        return result;
    }
}