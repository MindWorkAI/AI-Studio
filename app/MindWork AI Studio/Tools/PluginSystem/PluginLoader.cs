using System.Text;

using AIStudio.Settings;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Loads Lua modules from a plugin directory.
/// </summary>
/// <remarks>
/// Any plugin can load Lua modules from its own directory. This class is used to load these modules.
/// Loading other modules outside the plugin directory is not allowed.
/// </remarks>
/// <param name="pluginDirectory">The directory where the plugin is located.</param>
/// <param name="allowedBaseDirectory">
/// The directory the plugin directory must be nested in. Without it, the installed plugins directory
/// is used. Validating a plugin before its installation needs this, because the plugin is not
/// installed yet and lives in a staging directory outside the installed plugins directory.
/// </param>
public sealed class PluginLoader(string pluginDirectory, string? allowedBaseDirectory = null) : ILuaModuleLoader
{
    private static readonly string PLUGIN_BASE_PATH = Path.Join(SettingsManager.DataDirectory, "plugins");

    private readonly string baseDirectory = string.IsNullOrWhiteSpace(allowedBaseDirectory) ? PLUGIN_BASE_PATH : allowedBaseDirectory;

    #region Implementation of ILuaModuleLoader

    /// <inheritdoc />
    public bool Exists(string moduleName)
    {
        // Ensure that the user doesn't try to escape the plugin directory:
        if (moduleName.Contains("..") || pluginDirectory.Contains(".."))
            return false;

        // Ensure that the plugin directory is nested in the allowed base directory:
        if (!pluginDirectory.StartsWith(this.baseDirectory, StringComparison.OrdinalIgnoreCase))
            return false;

        var path = Path.Join(pluginDirectory, $"{moduleName}.lua");
        return File.Exists(path);
    }

    /// <inheritdoc />
    public async ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        var path = Path.Join(pluginDirectory, $"{moduleName}.lua");
        var code = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);

        return new(moduleName, code);
    }

    #endregion
}