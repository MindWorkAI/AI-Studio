using System.Text;
using System.Text.RegularExpressions;

using SharedTools;

namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Writes the complete plugin.lua of a direct chat launcher from its metadata and the settings a
/// user chose.
/// </summary>
/// <remarks>
/// <para>
/// A launcher needs no LLM to be changed: it has no system prompt, no UI, and no prompt builder.
/// The plugin loader stops reading those fields as soon as a launch behavior is present, so a
/// launcher is fully described by its top-level metadata plus a flat ASSISTANT table. That makes a
/// canonical rewrite lossless in behavior, which is what this writer produces.
/// </para>
/// <para>
/// It is not lossless in text: comments, formatting, and anything the file carries beyond that
/// shape are gone afterward. Callers must therefore check both CanRewrite and IsCanonicalSource
/// before offering the mechanical editing path, and fall back to the code editor or the AI revision
/// otherwise.
/// </para>
/// </remarks>
public static class DirectChatLauncherLuaWriter
{
    private const string PLUGIN_FILE_NAME = "plugin.lua";

    //
    // The plugin loader rejects empty authors, categories, and target groups. A plugin that is
    // running should have all of them, but a defective one must not turn into a file that cannot be
    // loaded back, hence these fallbacks. They mirror what the Assistant Builder generates.
    //
    private const string FALLBACK_AUTHOR = "MindWork AI - Assistant Builder";
    private const string FALLBACK_SUPPORT_CONTACT = "mailto:info@mindwork.ai";
    private const string FALLBACK_SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio";
    private const string FALLBACK_CATEGORY = nameof(PluginCategory.CORE);
    private const string FALLBACK_TARGET_GROUP = nameof(PluginTargetGroup.EVERYONE);

    //
    // An inline icon or a companion file would be dropped by a canonical rewrite, and neither is
    // recoverable from the loaded plugin: the icon is kept as a data URL, and companion files are
    // pulled in by Lua itself.
    //
    private static readonly Regex NON_CANONICAL_CONTENT = new(@"\bICON_SVG\b|\brequire\s*\(", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether this plugin is a locally managed launcher whose settings a user may edit at all.
    /// This check reads no files, so it is safe to call while rendering.
    /// </summary>
    public static bool CanRewrite(PluginAssistants plugin) =>
        plugin is { StartsChatDirectly: true, IsInternal: false, IsManagedByConfigServer: false } &&
        !string.IsNullOrWhiteSpace(plugin.PluginPath);

    /// <summary>
    /// Whether the current plugin.lua holds nothing a canonical rewrite would throw away.
    /// </summary>
    /// <param name="currentLua">The current plugin.lua content.</param>
    public static bool IsCanonicalSource(string currentLua) => !string.IsNullOrWhiteSpace(currentLua) && !NON_CANONICAL_CONTENT.IsMatch(currentLua);

    /// <summary>
    /// Whether the plugin directory holds a single plugin.lua and no companion Lua files.
    /// This one touches the file system, so keep it out of render paths.
    /// </summary>
    public static bool HasCompanionLuaFiles(PluginAssistants plugin) =>
        plugin.ReadAllLuaFiles().Keys.Any(relativePath => !string.Equals(relativePath, PLUGIN_FILE_NAME, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Writes the complete plugin.lua for an installed launcher whose settings changed.
    /// </summary>
    /// <param name="plugin">The installed launcher whose metadata is carried over.</param>
    /// <param name="definition">The name, title, description, and chat settings the user chose.</param>
    /// <returns>The plugin.lua content, ready to be validated and written.</returns>
    public static string Write(PluginAssistants plugin, DirectChatLauncherDefinition definition) =>
        Write(DirectChatLauncherPluginMetadata.FromPlugin(plugin), definition);

    /// <summary>
    /// Writes the complete plugin.lua for a launcher.
    /// </summary>
    /// <remarks>
    /// A launcher is fully described by its metadata plus a flat ASSISTANT table, so this is the
    /// whole file rather than a starting point. The Assistant Builder uses that: for a launcher it
    /// asks a model for the texts only and writes the file itself, because there is nothing left
    /// for a model to decide.
    /// </remarks>
    /// <param name="plugin">The metadata of the launcher, either carried over or newly chosen.</param>
    /// <param name="definition">The name, title, description, and chat settings the user chose.</param>
    /// <returns>The plugin.lua content, ready to be validated and written.</returns>
    public static string Write(DirectChatLauncherPluginMetadata plugin, DirectChatLauncherDefinition definition)
    {
        var builder = new StringBuilder();

        builder.AppendLine("--[[");
        builder.AppendLine("  This direct chat launcher is maintained by AI Studio: its settings dialog rewrites this");
        builder.AppendLine("  file as a whole. Editing it by hand works, but the next change made through the dialog");
        builder.AppendLine("  replaces everything below, including comments and formatting.");
        builder.AppendLine("]]");
        builder.AppendLine();

        builder.AppendLine("-- The ID for this plugin:");
        builder.AppendLine($"ID = \"{plugin.Id}\"");
        builder.AppendLine();

        builder.AppendLine("-- The name of the plugin:");
        builder.AppendLine($"NAME = \"{Escape(definition.PluginName)}\"");
        builder.AppendLine();

        builder.AppendLine("-- The description of the plugin:");
        builder.AppendLine($"DESCRIPTION = \"{Escape(definition.Description)}\"");
        builder.AppendLine();

        builder.AppendLine("-- The version of the plugin:");
        builder.AppendLine($"VERSION = \"{plugin.Version}\"");
        builder.AppendLine();

        builder.AppendLine("-- The type of the plugin:");
        builder.AppendLine($"TYPE = \"{nameof(PluginType.ASSISTANT)}\"");
        builder.AppendLine();

        builder.AppendLine("-- The authors of the plugin:");
        builder.AppendLine($"AUTHORS = {WriteStringList(plugin.Authors, FALLBACK_AUTHOR)}");
        builder.AppendLine();

        builder.AppendLine("-- The support contact for the plugin:");
        builder.AppendLine($"SUPPORT_CONTACT = \"{Escape(ValueOrFallback(plugin.SupportContact, FALLBACK_SUPPORT_CONTACT))}\"");
        builder.AppendLine();

        builder.AppendLine("-- The source URL for the plugin:");
        builder.AppendLine($"SOURCE_URL = \"{Escape(ValueOrFallback(plugin.SourceURL, FALLBACK_SOURCE_URL))}\"");
        builder.AppendLine();

        builder.AppendLine("-- The categories for the plugin:");
        builder.AppendLine($"CATEGORIES = {WriteEnumList(plugin.Categories, FALLBACK_CATEGORY)}");
        builder.AppendLine();

        builder.AppendLine("-- The target groups for the plugin:");
        builder.AppendLine($"TARGET_GROUPS = {WriteEnumList(plugin.TargetGroups, FALLBACK_TARGET_GROUP)}");
        builder.AppendLine();

        builder.AppendLine("-- The flag for whether the plugin is maintained:");
        builder.AppendLine($"IS_MAINTAINED = {WriteBoolean(plugin.IsMaintained)}");
        builder.AppendLine();

        builder.AppendLine("-- When the plugin is deprecated, this message will be shown to users:");
        builder.AppendLine($"DEPRECATION_MESSAGE = \"{Escape(plugin.DeprecationMessage)}\"");
        builder.AppendLine();

        builder.AppendLine("-- Enterprise-managed assistants cannot be revised with AI. Keep false for locally managed plugins:");
        builder.AppendLine("DEPLOYED_USING_CONFIG_SERVER = false");
        builder.AppendLine();

        //
        // This metadata marks assistants the Builder created and must not appear on manually
        // authored plugins, so it is carried over rather than always written:
        //
        if (plugin.IsAssistantBuilderGenerated)
        {
            builder.AppendLine("-- This assistant was created by the AI Studio Assistant Builder:");
            builder.AppendLine("AI_STUDIO_ASSISTANT_BUILDER = {Generated = true, SchemaVersion = 1}");
            builder.AppendLine();
        }

        builder.AppendLine("-- The tile opens a chat directly, hence it needs no system prompt, no submit text, and no UI:");
        builder.AppendLine("ASSISTANT = {");
        builder.AppendLine($"    [\"Title\"] = \"{Escape(definition.Title)}\",");
        builder.AppendLine($"    [\"Description\"] = \"{Escape(definition.Description)}\",");
        builder.AppendLine($"    [\"LaunchBehavior\"] = \"{nameof(AssistantPluginLaunchBehavior.OPEN_WORKSPACE_CHAT_BY_NAME)}\",");
        builder.AppendLine($"    [\"WorkspaceName\"] = \"{Escape(definition.Launch.WorkspaceName.Trim())}\",");

        //
        // Omitted IDs mean "use the chat defaults", while an empty GUID explicitly selects no
        // profile or no chat template. An empty provider GUID has no such meaning and is invalid:
        //
        if (definition.Launch.ProviderId is { } providerId && providerId != Guid.Empty)
            builder.AppendLine($"    [\"ProviderId\"] = \"{providerId}\",");

        if (definition.Launch.ProfileId is { } profileId)
            builder.AppendLine($"    [\"ProfileId\"] = \"{profileId}\",");

        if (definition.Launch.ChatTemplateId is { } chatTemplateId)
            builder.AppendLine($"    [\"ChatTemplateId\"] = \"{chatTemplateId}\",");

        if (definition.Launch.DataSourceIds is { Count: > 0 } dataSourceIds)
        {
            builder.AppendLine("    [\"DataSourceIds\"] = {");
            foreach (var dataSourceId in dataSourceIds)
                builder.AppendLine($"        \"{dataSourceId}\",");

            builder.AppendLine("    },");
        }

        if (definition.Launch.ToolIds is { Count: > 0 } toolIds)
        {
            builder.AppendLine("    [\"ToolIds\"] = {");
            foreach (var toolId in toolIds)
                builder.AppendLine($"        {LuaTools.ToLuaStringLiteral(toolId)},");

            builder.AppendLine("    },");
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string WriteStringList(IReadOnlyList<string> values, string fallback)
    {
        var usableValues = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        if (usableValues.Length == 0)
            usableValues = [fallback];

        return $"{{{string.Join(", ", usableValues.Select(value => $"\"{Escape(value)}\""))}}}";
    }

    private static string WriteEnumList<T>(IReadOnlyList<T> values, string fallback) where T : struct, Enum
    {
        var names = values.Select(value => Enum.GetName(value) ?? string.Empty).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
        if (names.Length == 0)
            names = [fallback];

        return $"{{{string.Join(", ", names.Select(name => $"\"{name}\""))}}}";
    }

    private static string WriteBoolean(bool value) => value ? "true" : "false";

    private static string ValueOrFallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    //
    // Titles, descriptions, and workspace names are free text. Lua has no raw newlines inside
    // quoted strings, so everything that would break out of one is escaped. The backslash must come
    // first, otherwise the escapes added afterwards would be escaped again:
    //
    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\t", "\\t", StringComparison.Ordinal);
}