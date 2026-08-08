using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginFactory));
    private static SettingsManager SettingsManagerAccess => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();

    private static string DATA_DIR = string.Empty;
    private static string PLUGINS_ROOT = string.Empty;
    private static string INTERNAL_PLUGINS_ROOT = string.Empty;

    /// <summary>
    /// The directory the config server downloads the configuration plugins of an organization into.
    /// </summary>
    /// <remarks>
    /// This is not the home of configuration plugins in general: a local configuration plugin can
    /// live in any directory below the plugins root. Only the IT department of an organization
    /// deploys plugins here, each in a directory named after its configuration ID.
    /// </remarks>
    private static string ENTERPRISE_CONFIGURATION_PLUGINS_ROOT = string.Empty;
    private static string HOT_RELOAD_LOCK_FILE = string.Empty;
    private static FileSystemWatcher HOT_RELOAD_WATCHER = null!;

    public static ILanguagePlugin BaseLanguage { get; private set; } = NoPluginLanguage.INSTANCE;

    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the enterprise encryption instance for decrypting API keys in configuration plugins.
    /// </summary>
    public static EnterpriseEncryption? EnterpriseEncryption { get; private set; }

    /// <summary>
    /// Initializes the enterprise encryption service by reading the encryption secret
    /// from the effective enterprise source.
    /// </summary>
    /// <param name="rustService">The Rust service to use for reading the encryption secret.</param>
    public static async Task InitializeEnterpriseEncryption(Services.RustService rustService)
    {
        var encryptionSecret = await rustService.EnterpriseEnvConfigEncryptionSecret();
        InitializeEnterpriseEncryption(encryptionSecret);
    }

    /// <summary>
    /// Initializes the enterprise encryption service using a prefetched secret value.
    /// </summary>
    /// <param name="encryptionSecret">The base64-encoded enterprise encryption secret.</param>
    public static void InitializeEnterpriseEncryption(string? encryptionSecret)
    {
        LOG.LogInformation("Initializing enterprise encryption service...");
        var enterpriseEncryptionLogger = Program.LOGGER_FACTORY.CreateLogger<EnterpriseEncryption>();
        EnterpriseEncryption = new EnterpriseEncryption(enterpriseEncryptionLogger, encryptionSecret);

        if (EnterpriseEncryption.IsAvailable)
            LOG.LogInformation("Enterprise encryption service is available.");
        else
            LOG.LogWarning("Enterprise encryption service is not available (no secret configured).");
    }

    /// <summary>
    /// Set up the plugin factory. We will read the data directory from the settings manager.
    /// Afterward, we will create the plugins directory and the internal plugin directory.
    /// </summary>
    public static bool Setup()
    {
        if(IsInitialized)
            return false;
        
        LOG.LogInformation("Initializing plugin factory...");
        DATA_DIR = SettingsManager.DataDirectory!;
        PLUGINS_ROOT = Path.Join(DATA_DIR, "plugins");
        HOT_RELOAD_LOCK_FILE = Path.Join(PLUGINS_ROOT, ".lock");
        INTERNAL_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".internal");
        ENTERPRISE_CONFIGURATION_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".config");
        
        if (!Directory.Exists(PLUGINS_ROOT))
            Directory.CreateDirectory(PLUGINS_ROOT);
        
        HOT_RELOAD_WATCHER = new(PLUGINS_ROOT);
        IsInitialized = true;
        LOG.LogInformation("Plugin factory initialized successfully.");
        return true;
    }

    /// <summary>
    /// Checks whether a plugin directory belongs to the enterprise configuration area.
    /// </summary>
    /// <remarks>
    /// Only the IT department of an organization deploys plugins there: the config server downloads
    /// them into a directory named after their configuration ID. We decide by path on purpose. The
    /// Lua field DEPLOYED_USING_CONFIG_SERVER is self-declared, so any plugin could claim to be
    /// deployed by an organization.
    /// </remarks>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <returns>True when the directory is nested in the enterprise configuration directory.</returns>
    public static bool IsEnterpriseConfigurationPath(string? pluginPath) => IsPathInside(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT, pluginPath);

    /// <summary>
    /// Checks whether a plugin directory is stored below the plugins directory of AI Studio.
    /// </summary>
    /// <remarks>
    /// Everything that removes or replaces plugin files checks this first, so a plugin directory
    /// which points somewhere else can never be touched.
    /// </remarks>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <returns>True when the directory is nested in the plugins directory.</returns>
    public static bool IsInsidePluginsRoot(string? pluginPath) => IsPathInside(PLUGINS_ROOT, pluginPath);

    private static bool IsPathInside(string rootDirectory, string? pluginPath)
    {
        if (string.IsNullOrWhiteSpace(pluginPath) || string.IsNullOrWhiteSpace(rootDirectory))
            return false;

        try
        {
            var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pluginDirectory = Path.GetFullPath(pluginPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return pluginDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            LOG.LogWarning(e, $"Was not able to check whether the plugin directory '{pluginPath}' is nested in '{rootDirectory}'. Treating it as unrelated.");
            return false;
        }
    }

    /// <summary>
    /// Checks whether a configuration plugin was deployed by the IT department of an organization.
    /// </summary>
    /// <remarks>
    /// A plugin which is deployed but could not be loaded still counts: it might be broken, e.g. due
    /// to invalid Lua code or an incomplete download, but it was not removed. Everything it manages
    /// stays under the control of the organization until the plugin is gone for good.
    /// </remarks>
    /// <param name="configPluginId">The ID of the configuration plugin.</param>
    /// <returns>True when the plugin belongs to an organization, false when it is local or unknown.</returns>
    public static bool IsEnterpriseConfigurationPlugin(Guid configPluginId)
    {
        if (configPluginId == Guid.Empty || !IsInitialized)
            return false;

        if (AVAILABLE_PLUGINS.Any(plugin => plugin.Id == configPluginId && plugin.Type is PluginType.CONFIGURATION && IsEnterpriseConfigurationPath(plugin.LocalPath)))
            return true;

        return Directory.Exists(Path.Join(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT, configPluginId.ToString()));
    }

    private static async Task LockHotReloadAsync()
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized.");
            return;
        }

        try
        {
            if (File.Exists(HOT_RELOAD_LOCK_FILE))
            {
                LOG.LogWarning("Hot reload lock file already exists.");
                return;
            }
            
            await File.WriteAllTextAsync(HOT_RELOAD_LOCK_FILE, DateTime.UtcNow.ToString("o"));
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while trying to lock hot reloading.");
        }
    }

    private static void UnlockHotReload()
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized.");
            return;
        }

        try
        {
            if(File.Exists(HOT_RELOAD_LOCK_FILE))
                File.Delete(HOT_RELOAD_LOCK_FILE);
            else
                LOG.LogWarning("Hot reload lock file does not exist. Nothing to unlock.");
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while trying to unlock hot reloading.");
        }
    }
    
    public static void Dispose()
    {
        if(!IsInitialized)
            return;
        
        HOT_RELOAD_WATCHER.Dispose();
    }

    public static IReadOnlyList<DataMandatoryInfo> GetMandatoryInfos()
    {
        return RUNNING_PLUGINS
            .OfType<PluginConfiguration>()
            .SelectMany(plugin => plugin.MandatoryInfos)
            .ToList();
    }

    public static IReadOnlyList<DataIntroduction> GetIntroductions()
    {
        return RUNNING_PLUGINS
            .OfType<PluginConfiguration>()
            .SelectMany(plugin => plugin.Introductions)
            .OrderBy(introduction => introduction.Index)
            .ToList();
    }
}