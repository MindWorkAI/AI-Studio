using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// What the user gets to see before a plugin archive is installed. It is not bound to a specific
/// plugin type, so it also serves upcoming import paths for other plugin types.
/// </summary>
/// <param name="Plugin">The plugin from the archive, with the metadata it declares about itself.</param>
/// <param name="ExistingPlugin">The installed plugin that gets replaced or null when the archive adds a new plugin.</param>
/// <param name="ConfigurationSummary">
/// What a configuration plugin would set up. Null for every other plugin type.
/// </param>
public sealed record PluginImportPreview(IPluginMetadata Plugin, IAvailablePlugin? ExistingPlugin, ConfigurationPluginImportSummary? ConfigurationSummary = null)
{
    /// <summary>
    /// True when an installed plugin with the same ID gets replaced.
    /// </summary>
    public bool ReplacesExisting => this.ExistingPlugin is not null;
}