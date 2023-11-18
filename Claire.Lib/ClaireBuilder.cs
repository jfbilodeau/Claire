using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace Claire;

public class ClaireBuilder
{
    private IConfigurationRoot? _configuration;

    public ClaireBuilder()
    {
    }

    public ClaireBuilder WithDefaultConfiguration(Assembly assembly)
    {
        if (_configuration != null)
        {
            throw new ApplicationException("Configuration already set");
        }

        // Build configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("Claire.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(assembly)
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

        // This check removes the warning about claireConfiguration being null
        if (claireConfiguration == null)
        {
            // Should never happen but just in case
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

        if (claireConfiguration.ChatHistorySize < 0)
        {
            throw new Exception("ChatHistorySize must be greater than or equal to 0");
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

        var claire = new Claire(claireConfiguration);

        return claire;
    }
}