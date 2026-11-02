namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Where the entries of a glossary of a configuration plugin come from.
/// </summary>
/// <remarks>
/// The plugin is the source of these entries. They never go into the settings, which hold only what
/// describes the glossary; the GlossaryStore keeps them in memory while the plugin is loaded.
/// </remarks>
/// <param name="Entries">The entries the plugin lists in its Lua table.</param>
/// <param name="File">The file within the plugin folder which holds the entries; null when the plugin lists them itself.</param>
public sealed record GlossaryPluginSource(IReadOnlyList<GlossaryEntry> Entries, GlossaryPluginFile? File = null);