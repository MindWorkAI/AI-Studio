using System.CommandLine.Help;

using Build.Commands;
using Build.Tools;

//
// A RootCommand would take the name of the executable, which is "build", and System.CommandLine refuses
// a root with the same name as one of its commands. Hence, the root is a command of its own, which
// brings only the help option of a RootCommand along:
//
var rootCommand = new Command("build-script", "Build, check, and release MindWork AI Studio. Run the commands from app/Build with 'dotnet run <command>'.")
{
    new HelpOption(),
    CheckRidsCommand.CreateCommand(),
    UpdateMetadataCommands.CreateReleaseCommand(),
    UpdateMetadataCommands.CreateRebuildReleaseCommand(),
    UpdateMetadataCommands.CreateUpdateMetainfoCommand(),
    UpdateMetadataCommands.CreateUpdateVersionsCommand(),
    UpdateMetadataCommands.CreatePrepareCommand(),
    UpdateMetadataCommands.CreateBuildCommand(),
    UpdateMetadataCommands.CreateUpdateProjectHashCommand(),
    UpdateWebAssetsCommand.CreateCommand(),
    CollectI18NKeysCommand.CreateCommand(),
    AssistantPluginHashCommand.CreateCommand(),
    VerifyModelsCommand.CreateCommand(),
    VerifyCommand.CreateCommand()
};

//
// A command which fails deep inside, e.g., the build within a release, ends the whole script through
// a CommandExitedException, once it has said why. System.CommandLine would print that exception with
// its stack trace, so we handle the exceptions ourselves and print only those nobody expected:
//
try
{
    return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (CommandExitedException exception)
{
    return exception.ExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Unhandled exception: {exception}");
    return 1;
}
