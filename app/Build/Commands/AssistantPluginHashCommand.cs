using SharedTools;

namespace Build.Commands;

public sealed class AssistantPluginHashCommand
{
    /// <summary>
    /// Defines the command "assistant-plugin-hash".
    /// </summary>
    public static Command CreateCommand()
    {
        var pluginDir = new Argument<string>("pluginDir") { Description = "Path to the assistant plugin directory" };
        var luaSnippet = new Option<bool>("--lua-snippet") { Description = "Also print a Lua snippet for CONFIG[\"SETTINGS\"]" };
        var command = new Command("assistant-plugin-hash", "Compute the canonical assistant-plugin hash for a plugin directory") { pluginDir, luaSnippet };
        command.SetAction(parseResult => new AssistantPluginHashCommand().ComputeAssistantPluginHash(parseResult.GetRequiredValue(pluginDir), parseResult.GetValue(luaSnippet)));
        return command;
    }

    public void ComputeAssistantPluginHash(string pluginDir, bool luaSnippet = false)
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
