namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// The plugin metadata a direct chat launcher carries beyond its chat settings.
/// </summary>
/// <remarks>
/// An installed launcher keeps these in its plugin.lua, and editing one carries them over. A
/// launcher the Assistant Builder is about to create has no file yet, so its metadata comes from
/// the Builder's defaults instead. Both paths end in the same writer, which is where the two meet.
/// </remarks>
/// <param name="Id">The plugin ID, which stays with the plugin for its whole life.</param>
/// <param name="Version">The plugin version, as it appears in the Lua file.</param>
/// <param name="Authors">The authors of the plugin.</param>
/// <param name="SupportContact">Where users turn with questions about this plugin.</param>
/// <param name="SourceURL">Where the plugin comes from.</param>
/// <param name="Categories">The categories this plugin belongs to.</param>
/// <param name="TargetGroups">The target groups this plugin is meant for.</param>
/// <param name="IsMaintained">Whether the plugin is still maintained.</param>
/// <param name="DeprecationMessage">What users are told when the plugin is deprecated.</param>
/// <param name="IsAssistantBuilderGenerated">Whether the Assistant Builder created this plugin.</param>
public sealed record DirectChatLauncherPluginMetadata(Guid Id, string Version, IReadOnlyList<string> Authors, string SupportContact, string SourceURL,
    IReadOnlyList<PluginCategory> Categories, IReadOnlyList<PluginTargetGroup> TargetGroups, bool IsMaintained, string DeprecationMessage, bool IsAssistantBuilderGenerated)
{
    /// <summary>
    /// Takes the metadata of an installed launcher for the case where one is edited.
    /// </summary>
    public static DirectChatLauncherPluginMetadata FromPlugin(PluginAssistants plugin) => new(plugin.Id, plugin.Version.ToString(), plugin.Authors,
        plugin.SupportContact, plugin.SourceURL, plugin.Categories, plugin.TargetGroups, plugin.IsMaintained, plugin.DeprecationMessage, plugin.IsAssistantBuilderGenerated);
}