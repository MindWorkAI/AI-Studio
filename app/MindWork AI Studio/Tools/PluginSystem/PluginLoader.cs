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
public sealed class PluginLoader(string pluginDirectory) : ILuaModuleLoader
{
    private static readonly string PLUGIN_BASE_PATH = Path.Join(SettingsManager.DataDirectory, "plugins");

    #region Implementation of ILuaModuleLoader

    /// <inheritdoc />
    public bool Exists(string moduleName)
    {
        // Ensure that the user doesn't try to escape the plugin directory:
        if (moduleName.Contains("..") || pluginDirectory.Contains(".."))
            return false;
        
        // Ensure that the plugin directory is nested in the plugin base path:
        if (!pluginDirectory.StartsWith(PLUGIN_BASE_PATH, StringComparison.OrdinalIgnoreCase))
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