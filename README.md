## Claire

Welcome to Claire, the **C**ommand-**L**ine **AI** **R**untime **E**nvironment

### What is Claire?

Claire is a proof of concept as well as a demo of Azure OpenAI and prompt engineering. Claire provide a natural-language, conversational interface to the command line.
- [x] Claire is able to suggest commands based on natural-language user request
- [x] Claire is able to execute commands and return the results to the user
- [x] Should the command issue an error (`stderr`), Claire will attempt to explain the error to the user
- [x] Users are able to request explanation of commands and errors
- [x] Claire can generate configuration files and scripts and save them to the user's computer
- [x] Claire is written in .NET 8.0 runs on Linux, MacOS, and Windows

### Limitations and Caveats

- Claire is running on the user's computer. **Please read the command proposed by Claire carefully before executing it.** The author is not responsible to any damage caused by the execution of the commands proposed by Claire.
- Claire is not able to execute commands that require user input. For example, `sudo` commands that require the user to enter a password will not work.
- Claire is able to generate and save files--but files will be save in the working directory of the application--not the current directory of the shell. For example, if start Claire in `/home/user`, then ask Claire to `cd ./Documents` and you ask Claire to generate a file, the file will be saved in `/home/user`. Not `/home/user/Document`. This is a limitation of the current implementation.
- Claire is an AI so it's bound to make mistakes. Of course, all mistakes are caused by the AI and certainly not by the author of the application. 😉

### How to use Claire

#### Prerequisites
As of now, there is no binary distribution of Claire. Source only. You will need the [.NET 8.0 SDK (dotnet.microsoft.com/download/dotnet/8.0)](https://dotnet.microsoft.com/download/dotnet/8.0). 

You will also need an Azure account with OpenAI enabled. You can sign up for a free account [free account (azure.microsoft.com/free/)](https://azure.microsoft.com/free/).

#### Steps
1. `git clone https://github.com/jfbilodeau/Claire.git`
2. `cd ./Claire/Claire`
3. Edit `Claire.json` and add the following configuration:
 - `OpenAiUrl`: Your OpenAI API key
 - `OpenAIKey`: Your OpenAI API key
 - `OpenAIModel`: The name of the model to use
 - `process`: (Optional) The name of the shell to use. Default is `bash` on Linux and MacOS. `cmd` on Windows.
4. `dotnet run`

