using Claire;

Console.WriteLine("Starting Claire...");

// Create Claire

var claire = new ClaireBuilder()
    .WithDefaultConfiguration(typeof(Program).Assembly)
    .Build();

await claire.Run();

Console.WriteLine("Thank you for using Claire!");