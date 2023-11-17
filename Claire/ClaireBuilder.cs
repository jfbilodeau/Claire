using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace Claire;

public class ClaireBuilder
{
    private IConfiguration? _configuration;

    public ClaireBuilder()
    {
    }

    public ClaireBuilder WithDefaultConfiguration()
    {
        if (_configuration != null)
        {
            throw new ApplicationException("Configuration already set");
        }
        
        // Build configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("claire.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<ClaireConfiguration>()
            .Build();
        
        return this;
    }

    public ClaireBuilder WithConfiguration(IConfiguration configuration)
    {
        if (_configuration != null)
        {
            throw new ApplicationException("Configuration already set");
        }
        
        return this;
    }

    public Claire Build()
    {
        if (_configuration == null)
        {
            throw new ClaireException("WithConfiguration or WithDefaultConfiguration must be called before Build");
        }
        
        var claireConfiguration = _configuration.Get<ClaireConfiguration>();

        if (claireConfiguration == null)
        {
            // Should never happen but just in case
            // This check removes the warning about claireConfiguration being null
            throw new ApplicationException("Failed to create Claire configuration");
        }

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

        if (string.IsNullOrEmpty(claireConfiguration.ShellProcessName))
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

        var claire = new Claire(claireConfiguration);

        return claire;
    }
}