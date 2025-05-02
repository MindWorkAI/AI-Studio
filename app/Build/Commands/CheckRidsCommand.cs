// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Build.Commands;

public sealed class CheckRidsCommand
{
    [Command("check-rids", Description = "Check the RIDs for the current OS")]
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