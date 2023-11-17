using System.Runtime.InteropServices;
using Claire;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Starting Clair...");

// Create Claire

var claire = new ClaireBuilder()
    .WithDefaultConfiguration()
    .Build();

await claire.Run();

Console.WriteLine("Thank you for using Claire!");