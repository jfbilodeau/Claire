using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Starting...");

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("claire.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<ClaireConfiguration>()
    .Build();

var claireConfiguration = configuration.Get<ClaireConfiguration>();

if (claireConfiguration == null) {
    Console.Error.WriteLine("Failed to create Claire configuration (should never happen)");

    return;
}   

if (string.IsNullOrEmpty(claireConfiguration.Process)) {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        claireConfiguration.Process = "cmd.exe";
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
        claireConfiguration.Process = "bash";
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
        claireConfiguration.Process = "bash";
    } else {
        throw new Exception("Unknown OS and `process` configuration not set");
    }
}

// Create Claire
var claire = new Claire(claireConfiguration);

await claire.Run();

Console.WriteLine("Thank you for using Claire!");