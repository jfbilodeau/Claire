using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit.Abstractions;

namespace Claire.Test;

public class TestCommand
{
    private readonly ITestOutputHelper _testOutputHelper;

    public TestCommand(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("How do I change directory?", "cd")]
    [InlineData("How do I create a new directory?", "(md|mkdir)")]
    [InlineData("How do I create a new directory called example?", "(md|mkdir) example")]
    [InlineData("Delete a file called test.txt", "(rm|del) test.txt")]
    [InlineData("Close the command prompt", "exit")]
    public async void ExecuteCommand(string prompt, string expectedCommand)
    {
        // var claire = new ClaireBuilder(new TestUserInterface()).WithDefaultConfiguration(typeof(TestCommand).Assembly).Build();
        //
        // var result = await claire.(prompt);
        //
        // Assert.Equal(ChatResponseType.Command, result.Type);
        // Assert.Matches(expectedCommand, result.Response);
    }
}