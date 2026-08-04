using SharedTools;

namespace Build.Commands;

public sealed class AssistantPluginHashCommand
{
    [Command("assistant-plugin-hash", Description = "Compute the canonical assistant-plugin hash for a plugin directory")]
    public void ComputeAssistantPluginHash(
        [Argument(Description = "Path to the assistant plugin directory")] string pluginDir,
        [Option("lua-snippet", Description = "Also print a Lua snippet for CONFIG[\"SETTINGS\"]")] bool luaSnippet = false)
    {
        if (!Environment.IsWorkingDirectoryValid())
            return;

        var resolvedPath = Path.GetFullPath(pluginDir, Directory.GetCurrentDirectory());
        if (!Directory.Exists(resolvedPath))
        {
            Console.WriteLine($"- Error: The plugin directory '{resolvedPath}' does not exist.");
            return;
        }

        var pluginHash = AssistantPluginHash.Compute(resolvedPath);
        if (string.IsNullOrWhiteSpace(pluginHash))
        {
            Console.WriteLine($"- Error: No Lua files were found in '{resolvedPath}'.");
            return;
        }

        Console.WriteLine(pluginHash);

        if (!luaSnippet)
            return;

        var displayName = Path.GetFileName(resolvedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var approvedAtUtc = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        Console.WriteLine();
        Console.WriteLine("""CONFIG["SETTINGS"]["DataAssistantPluginAudit.EnterpriseApprovedPlugins"] = {""");
        Console.WriteLine("    {");
        Console.WriteLine($"""        ["PluginHash"] = "{pluginHash}",""");
        Console.WriteLine($"""        ["DisplayName"] = "{displayName}",""");
        Console.WriteLine("""        ["Comment"] = "<optional comment>",""");
        Console.WriteLine("""        ["ApprovedBy"] = "<optional approver>",""");
        Console.WriteLine($"""        ["ApprovedAtUtc"] = "{approvedAtUtc}",""");
        Console.WriteLine("    }");
        Console.WriteLine("}");
    }
}
