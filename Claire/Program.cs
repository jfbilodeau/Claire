using Claire;

Console.Title = "Claire";

Console.WriteLine("Starting Claire...");

// Create Claire

var claire = new ClaireBuilder(new ConsoleUserInterface())
    .WithDefaultConfiguration(typeof(Program).Assembly)
    .WithCommandLine(args)
    .Build();

await claire.Run();

Console.WriteLine("Thank you for using Claire!");