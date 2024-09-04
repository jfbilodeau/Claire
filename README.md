## Claire

Welcome to Claire, the **C**ommand-**L**ine **A**rtificial **I**ntelligence **R**untime **E**nvironment

### What is Claire?

In her own words:
```
> Who are you?
I am Claire, a virtual assistant designed to guide users in using the Bash shell and the Azure CLI. I will assist you in writing shell scripts, creating and managing configuration files, explaining command-line features, and using the Azure platform for infrastructure and application management.
```

Claire is a proof of concept as well as a demo of Azure OpenAI and prompt engineering. Claire provide a natural-language, conversational interface to the command line.
- Claire is able to suggest commands based on natural-language user request
- Claire is able to execute commands and provide the results to the user
- Claire supports the Windows command line (`cmd` -- aka DOS Prompt), PowerShell, and Bash
- Claire attemps to automatically explain errors when a command issues errors via `stderr`)
- Users are able to request explanation of commands and errors
- Claire can generate configuration files (ie: Bicep) and scripts (`.bat`, `.ps1`, etc). Claire can also save them to the user's computer
- Claire is able to assist with the [AZ CLI (https://learn.microsoft.com/en-us/cli/azure/)](https://learn.microsoft.com/en-us/cli/azure/)
- Claire is written in .NET 8.0 runs on Linux, MacOS, and Windows
- Claire can be containerized to run in a sandboxed environment

### Semantic Kernel
Since 2024-09-04, Claire has been ported to Semantic Kernel by Microsoft. This allowed me to cut the code of the main program file, `Claire.cs` by half (668 loc -> 332 loc) and make Claire even more intelligent and interactive.

[Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/)

### Limitations and Caveats

- Claire is running on the user's computer. **Please read the command proposed by Claire carefully before executing it.** Claire is not sandboxed and can execute any command that the user can execute.
- Claire is not able to execute commands that require user input. For example, `sudo` commands that require the user to enter a password will not work and will cause Claire to hang while it's waiting for the command to complete.
- Claire is able to generate and save files--but files will be save in the working directory of the application--not the current directory of the shell. For example, if you start Claire in `/home/user`, then ask Claire to `cd ./Documents` and you ask Claire to generate a file, the file will be saved in `/home/user`. Not `/home/user/Document`. This is a limitation of the current implementation.
- Claire is an AI so it's bound to make mistakes. Of course, all mistakes are caused by the AI and certainly not by the author of the application. 😉

### How to use Claire

#### Prerequisites
As of now, there is no binary distribution of Claire. Source only. You will need the [.NET 8.0 SDK (dotnet.microsoft.com/download/dotnet/8.0)](https://dotnet.microsoft.com/download/dotnet/8.0). 

You will also need an Azure account with OpenAI enabled. You can sign up for a free account [free account (azure.microsoft.com/free/)](https://azure.microsoft.com/free/).

#### Quick start

In a terminal

1. `git clone https://github.com/jfbilodeau/Claire.git`
2. `cd ./Claire/Claire`
3. `dotnet run --OpenAiUrl URL --OpenAiKey KEY --OpenAiModel MODEL_NAME`


The following parameters are supported by Claire:

| Parameter | Type | Required | Description  |
|-|-|-|-|
|OpenAiUrl|`string`|Yes|The URL of the OpenAI API                                                                                |
|OpenAiKey|`string`|Yes|The key of the OpenAI API                                                                                |
|OpenAiModel|`string`|Yes|The name of the OpenAI model to use                                                                      |
|ShellProcessName|`string`|No|The name of the shell to use. Default is `bash` on Linux and MacOS. `cmd` on Windows. PowerShell is also supported.|
|ChatHistorySize|`int`|No|The number of chat history to keep and re-send to Azure OpenAI. This maintains a limited conversation state between the user and Azure OpenAI. Default is 10.|

Parameters may be passed to Claires using the following methods:
- Provide them in `<project-root>/Claire/Claire/Claire.csproj`
- Pass command line parameters. For example: `dotnet run --OpenAiUrl="https://..." --OpenAiKey="..." --OpenAiModel="..."`
- Environment variables (Necessary for containerization)
- (for developers) [.NET user secrets (https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

### Docker
Claire is not sandboxed. Be careful what you ask it to do!

```
Starting Claire...
Welcome to Claire. Where would you like to go today?
Please tell me what you would like to do?
What command should I avoid running on Windows?
I believe the command you are looking for is:
`del /F /S /Q C:\*`

Shall I executed it for you? Y/N: Y
```
😱

To run Claire in a sandbox, consider using a container. A `Dockerfile` is provided to containerize Claire.

1. `docker build -t claire .`
2. `docker run -it claire`


## Question/Comments
For any problems, please open an issue on GitHub. I will try to answer as soon as possible.

For any questions, please email me at [jbilodeau@microsoft.com](mailto:jfbilodeau@chronogears.com).

## License
MIT (see `./LICENSE.txt`)
