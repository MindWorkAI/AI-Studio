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
    VerifyCommand.CreateCommand(),
    FlatpakCommand.CreateCommand()
};

//
// A command which fails deep inside, e.g., the build within a release, ends the whole script through
// a CommandExitedException, once it has said why. System.CommandLine would print that exception with
// its stack trace, so we handle the exceptions ourselves and print only those nobody expected.
//
// System.CommandLine would also answer Ctrl+C by cancelling a token and ending the script two seconds
// later, whether the command has finished or not. No command reads that token, and the command flatpak
// needs longer to remove its container and volume. Without the timeout, Ctrl+C ends a command at once,
// unless the command handles Ctrl+C itself, as flatpak does:
//
try
{
    return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false, ProcessTerminationTimeout = null });
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
