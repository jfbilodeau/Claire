using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace Claire;

public class ClaireBuilder
{
    private readonly IUserInterface _userInterface;
    private readonly ConfigurationBuilder _configurationBuilder = new ConfigurationBuilder();

    public ClaireBuilder(IUserInterface userInterface)
    {
        _userInterface = userInterface;
    }  

    public ClaireBuilder WithDefaultConfiguration(Assembly assembly)
    { 
        // Build configuration
        _configurationBuilder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Claire.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(assembly)
            .Build();

        return this;
    }

    public ClaireBuilder WithConfiguration(IConfiguration configuration)
    {
        _configurationBuilder.AddConfiguration(configuration);
        
        return this;
    }

    public ClaireBuilder WithCommandLine(string[] args)
    {
        _configurationBuilder.AddCommandLine(args);

        return this;
    }

    public Claire Build()
    {
        var configuration = _configurationBuilder.Build();

        var claireConfiguration = configuration.Get<ClaireConfiguration>() 
            ?? throw new ApplicationException("Failed to create Claire configuration");

        // Validate configuration
        if (string.IsNullOrWhiteSpace(claireConfiguration.OpenAiUrl))
        {
            throw new Exception("OpenAiUrl is required");
        }

        if (string.IsNullOrWhiteSpace(claireConfiguration.OpenAiKey))
        {
            throw new Exception("OpenAiKey is required");
        }

        if (string.IsNullOrWhiteSpace(claireConfiguration.OpenAiModel))
        {
            throw new Exception("OpenAIModel is required");
        }

        if (string.IsNullOrWhiteSpace(claireConfiguration.ShellProcessName))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                claireConfiguration.ShellProcessName = "cmd.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                claireConfiguration.ShellProcessName = "bash";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                claireConfiguration.ShellProcessName = "bash";
            }
            else
            {
                throw new Exception("Unknown OS and `process` configuration not set");
            }
        }

        var claire = new Claire(claireConfiguration, _userInterface);

        return claire;
    }
}