namespace Build.Commands;

public sealed class CheckRidsCommand
{
    /// <summary>
    /// Defines the command "check-rids".
    /// </summary>
    public static Command CreateCommand()
    {
        var command = new Command("check-rids", "Check the RIDs for the current OS");
        command.SetAction(_ => new CheckRidsCommand().GetRids());
        return command;
    }

    public void GetRids()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;
        
        var rids = Environment.GetRidsForCurrentOS();
        Console.WriteLine("The following RIDs are available for the current OS:");
        foreach (var rid in rids)
        {
            Console.WriteLine($"- {rid}");
        }

        Console.WriteLine();
        Console.WriteLine("The RID for the current OS and CPU is:");
        var currentRid = Environment.GetCurrentRid();
        Console.WriteLine($"- {currentRid}");
    }
}