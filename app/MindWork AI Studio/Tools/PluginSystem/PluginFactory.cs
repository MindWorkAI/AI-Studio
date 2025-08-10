using AIStudio.Settings;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(PluginFactory));
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    
    private static bool IS_INITIALIZED;
    private static string DATA_DIR = string.Empty;
    private static string PLUGINS_ROOT = string.Empty;
    private static string INTERNAL_PLUGINS_ROOT = string.Empty;
    private static string CONFIGURATION_PLUGINS_ROOT = string.Empty;
    private static string HOT_RELOAD_LOCK_FILE = string.Empty;
    private static FileSystemWatcher HOT_RELOAD_WATCHER = null!;
    private static ILanguagePlugin BASE_LANGUAGE_PLUGIN = NoPluginLanguage.INSTANCE;

    public static ILanguagePlugin BaseLanguage => BASE_LANGUAGE_PLUGIN;

    /// <summary>
    /// Set up the plugin factory. We will read the data directory from the settings manager.
    /// Afterward, we will create the plugins directory and the internal plugin directory.
    /// </summary>
    public static bool Setup()
    {
        if(IS_INITIALIZED)
            return false;
        
        LOG.LogInformation("Initializing plugin factory...");
        DATA_DIR = SettingsManager.DataDirectory!;
        PLUGINS_ROOT = Path.Join(DATA_DIR, "plugins");
        HOT_RELOAD_LOCK_FILE = Path.Join(PLUGINS_ROOT, ".lock");
        INTERNAL_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".internal");
        CONFIGURATION_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".config");
        
        if (!Directory.Exists(PLUGINS_ROOT))
            Directory.CreateDirectory(PLUGINS_ROOT);
        
        HOT_RELOAD_WATCHER = new(PLUGINS_ROOT);
        IS_INITIALIZED = true;
        LOG.LogInformation("Plugin factory initialized successfully.");
        return true;
    }

    private static async Task LockHotReloadAsync()
    {
        if (!IS_INITIALIZED)
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
        if (!IS_INITIALIZED)
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
        if(!IS_INITIALIZED)
            return;
        
        HOT_RELOAD_WATCHER.Dispose();
    }
}