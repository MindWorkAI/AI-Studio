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

    /// <summary>
    /// The directory administrators use to try out a configuration before their organization deploys it.
    /// </summary>
    /// <remarks>
    /// Everything stored here acts on behalf of the organization, so that a test behaves like the
    /// later rollout, including the approval of assistant plugins. In exchange, the directory is
    /// emptied on every start: a test configuration lives for one session only. It also never gets
    /// the protection of a deployed configuration, so users can remove or replace it through the user
    /// interface.
    /// </remarks>
    private static string ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT = string.Empty;

    private static string HOT_RELOAD_LOCK_FILE = string.Empty;
    private static FileSystemWatcher HOT_RELOAD_WATCHER = null!;

    /// <summary>
    /// How many test configurations were removed while AI Studio was starting.
    /// </summary>
    /// <remarks>
    /// The user interface reports this: an administrator who placed a test configuration and restarted
    /// AI Studio would otherwise face an empty directory without any explanation.
    /// </remarks>
    public static int RemovedTestConfigurationsAtStartup { get; private set; }

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
        ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT = Path.Join(PLUGINS_ROOT, ".config-tests");

        if (!Directory.Exists(PLUGINS_ROOT))
            Directory.CreateDirectory(PLUGINS_ROOT);

        ClearTestConfigurationPlugins();
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
    /// Checks whether a plugin directory belongs to the test configuration area.
    /// </summary>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <returns>True when the directory is nested in the test configuration directory.</returns>
    public static bool IsEnterpriseTestConfigurationPath(string? pluginPath) => IsPathInside(ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT, pluginPath);

    /// <summary>
    /// Checks whether a plugin acts on behalf of an organization, either deployed by a configuration
    /// server or staged for a test.
    /// </summary>
    /// <remarks>
    /// Use this wherever a configuration speaks for the organization, e.g. when it approves assistant
    /// plugins or claims a setting against a local configuration plugin. Do not use it where a
    /// deployed configuration is protected against the user, e.g. against deletion: an administrator
    /// must be able to get rid of their own test configuration.
    /// </remarks>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <returns>True when the directory belongs to the enterprise or the test configuration area.</returns>
    public static bool IsOrganizationConfigurationPath(string? pluginPath) => IsEnterpriseConfigurationPath(pluginPath) || IsEnterpriseTestConfigurationPath(pluginPath);

    /// <summary>
    /// Determines which deployed configuration a plugin below the enterprise configuration directory
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// A configuration server downloads each configuration into a directory named after its ID. That
    /// archive may carry more than the configuration itself: organizations deploy assistant plugins
    /// and other plugin types alongside it, each in its own subdirectory. We therefore look at the
    /// topmost directory below the enterprise configuration directory instead of the directory the
    /// plugin lives in, which for such a plugin is a nested one.
    /// </remarks>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <param name="configurationId">The ID of the configuration the plugin was deployed with.</param>
    /// <returns>True when the plugin is nested in a directory named after a configuration ID.</returns>
    public static bool TryGetDeployedConfigurationId(string? pluginPath, out Guid configurationId)
    {
        configurationId = Guid.Empty;
        if (!IsEnterpriseConfigurationPath(pluginPath))
            return false;

        try
        {
            var root = Path.GetFullPath(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT);
            var relativePath = Path.GetRelativePath(root, Path.GetFullPath(pluginPath!));
            var deploymentDirectory = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

            return Guid.TryParse(deploymentDirectory, out configurationId) && configurationId != Guid.Empty;
        }
        catch (Exception e)
        {
            LOG.LogWarning(e, $"Was not able to determine the deployed configuration ID for the plugin directory '{pluginPath}'.");
            return false;
        }
    }

    /// <summary>
    /// Ranks how much say a configuration plugin has, based on where it is stored. The higher rank
    /// wins when two configuration plugins claim the same plugin ID.
    /// </summary>
    /// <remarks>
    /// A test configuration outranks a deployed one on purpose: an administrator tries out the next
    /// version of a configuration under the ID it will have later. Local configuration plugins rank
    /// lowest, so nobody can push aside what an organization deployed.
    /// </remarks>
    private static int GetConfigurationAuthority(string? pluginPath)
    {
        if (IsEnterpriseTestConfigurationPath(pluginPath))
            return 2;

        return IsEnterpriseConfigurationPath(pluginPath) ? 1 : 0;
    }

    /// <summary>
    /// Empties the test configuration directory.
    /// </summary>
    /// <remarks>
    /// A test configuration carries the rights of an organization configuration without anybody having
    /// deployed it. It must therefore never outlive the session it was placed in, and administrators
    /// get a predictable lifetime instead of a configuration which is swept away at some point.
    /// </remarks>
    private static void ClearTestConfigurationPlugins()
    {
        RemovedTestConfigurationsAtStartup = 0;
        try
        {
            if (Directory.Exists(ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT))
            {
                var removedTestConfigurations = Directory.EnumerateDirectories(ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT).Count();
                Directory.Delete(ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT, true);
                RemovedTestConfigurationsAtStartup = removedTestConfigurations;

                if (removedTestConfigurations > 0)
                    LOG.LogWarning($"Removed {removedTestConfigurations} test configuration(s) from '{ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT}'. Test configurations are valid for one session only.");
            }

            Directory.CreateDirectory(ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT);
        }
        catch (Exception e)
        {
            LOG.LogError(e, $"Failed to empty the test configuration directory '{ENTERPRISE_TEST_CONFIGURATION_PLUGINS_ROOT}'.");
        }
    }

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

    /// <summary>
    /// Checks whether a plugin directory is the plugins directory itself.
    /// </summary>
    /// <remarks>
    /// A `plugin.lua` placed directly in the plugins directory makes that directory the plugin
    /// directory. Removing or replacing such a plugin means touching its directory, which would take
    /// every other plugin with it.
    /// </remarks>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <returns>True when the directory is the plugins directory.</returns>
    public static bool IsPluginsRoot(string? pluginPath)
    {
        if (string.IsNullOrWhiteSpace(pluginPath) || string.IsNullOrWhiteSpace(PLUGINS_ROOT))
            return false;

        try
        {
            var root = Path.GetFullPath(PLUGINS_ROOT).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var pluginDirectory = Path.GetFullPath(pluginPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(root, pluginDirectory, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            LOG.LogWarning(e, $"Was not able to check whether the plugin directory '{pluginPath}' is the plugins directory. Treating it as the plugins directory.");
            return true;
        }
    }

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

    /// <summary>
    /// Checks whether a configuration plugin speaks for an organization: either deployed by its IT
    /// department, or staged as a test configuration.
    /// </summary>
    /// <remarks>
    /// A test configuration is only ever loaded, never merely present: it is emptied on every start,
    /// so there is no unloadable leftover to account for.
    /// </remarks>
    /// <param name="configPluginId">The ID of the configuration plugin.</param>
    /// <returns>True when the plugin speaks for an organization, false when it is local or unknown.</returns>
    public static bool IsOrganizationConfigurationPlugin(Guid configPluginId)
    {
        if (configPluginId == Guid.Empty || !IsInitialized)
            return false;

        if (IsEnterpriseConfigurationPlugin(configPluginId))
            return true;

        return AVAILABLE_PLUGINS.Any(plugin => plugin.Id == configPluginId && plugin.Type is PluginType.CONFIGURATION && IsEnterpriseTestConfigurationPath(plugin.LocalPath));
    }

    /// <summary>
    /// Counts how many operations currently write to the plugins directory.
    /// </summary>
    /// <remarks>
    /// Downloading an organization's configuration and installing a plugin can run at the same
    /// time. Without counting, whichever finishes first would unlock hot reloading while the other
    /// is still writing.
    /// </remarks>
    private static int HOT_RELOAD_LOCK_COUNT;
    private static readonly SemaphoreSlim HOT_RELOAD_LOCK_SEMAPHORE = new(1, 1);

    /// <summary>
    /// Holds back hot reloading while the caller writes to the plugins directory.
    /// </summary>
    /// <remarks>
    /// Every caller has to release the lock again, so wrap the write in a try-finally block. Hot
    /// reloading resumes once the last caller has released it.
    /// </remarks>
    public static async Task LockHotReloadAsync()
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized.");
            return;
        }

        await HOT_RELOAD_LOCK_SEMAPHORE.WaitAsync();
        try
        {
            if (HOT_RELOAD_LOCK_COUNT++ > 0)
                return;

            await File.WriteAllTextAsync(HOT_RELOAD_LOCK_FILE, DateTime.UtcNow.ToString("o"));
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while trying to lock hot reloading.");
        }
        finally
        {
            HOT_RELOAD_LOCK_SEMAPHORE.Release();
        }
    }

    /// <summary>
    /// Releases the hot reload lock of one caller, see LockHotReloadAsync.
    /// </summary>
    public static void UnlockHotReload()
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized.");
            return;
        }

        HOT_RELOAD_LOCK_SEMAPHORE.Wait();
        try
        {
            //
            // The count can be zero when the reload gave up waiting and removed the lock file
            // itself. We must not go negative, because that would keep the next lock from ever
            // writing the file again:
            //
            if (HOT_RELOAD_LOCK_COUNT > 0)
                HOT_RELOAD_LOCK_COUNT--;

            if (HOT_RELOAD_LOCK_COUNT > 0)
                return;

            if(File.Exists(HOT_RELOAD_LOCK_FILE))
                File.Delete(HOT_RELOAD_LOCK_FILE);
            else
                LOG.LogWarning("Hot reload lock file does not exist. Nothing to unlock.");
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while trying to unlock hot reloading.");
        }
        finally
        {
            HOT_RELOAD_LOCK_SEMAPHORE.Release();
        }
    }
    
    public static void Dispose()
    {
        if(!IsInitialized)
            return;

        HOT_RELOAD_WATCHER.Dispose();
        HOT_RELOAD_DEBOUNCE_TIMER.Dispose();
    }

    public static IReadOnlyList<DataMandatoryInfo> GetMandatoryInfos()
    {
        return ResolveLivePluginContent<DataMandatoryInfo>("mandatory info", plugin => plugin.MandatoryInfos).ToList();
    }

    public static IReadOnlyList<DataIntroduction> GetIntroductions()
    {
        return ResolveLivePluginContent<DataIntroduction>("introduction", plugin => plugin.Introductions)
            .OrderBy(introduction => introduction.Index)
            .ThenBy(introduction => introduction.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Collects live content from all running configuration plugins, so that each content ID appears exactly once.
    /// </summary>
    /// <remarks>
    /// The IDs of live content are chosen by whoever writes the configuration, so two configuration
    /// plugins may use the same ID. We resolve such a collision the same way a collision on a setting
    /// is resolved: a configuration which acts on behalf of the organization wins, so nobody can push
    /// aside what an organization deployed. Among configurations of the same origin, the declared
    /// priority decides, and when even that is equal, the plugin which started later wins.<br/><br/>
    /// Duplicates are not merely a cosmetic problem: the home page keys its panels by the introduction
    /// ID, and the acceptance of a mandatory info is stored per ID as well.
    /// </remarks>
    /// <param name="contentKind">The kind of content, used to report a collision in the log.</param>
    /// <param name="selector">Selects the content of one configuration plugin.</param>
    /// <typeparam name="T">The type of the live plugin content.</typeparam>
    /// <returns>The content of all configuration plugins, with every ID resolved to one winner.</returns>
    private static IEnumerable<T> ResolveLivePluginContent<T>(string contentKind, Func<PluginConfiguration, IEnumerable<T>> selector) where T : ILivePluginContent
    {
        var contentById = new Dictionary<string, (T Content, int Authority, int Priority)>(StringComparer.Ordinal);
        foreach (var plugin in RUNNING_PLUGINS.OfType<PluginConfiguration>())
        {
            var authority = GetConfigurationAuthority(plugin.PluginPath);
            foreach (var content in selector(plugin))
            {
                if (contentById.TryGetValue(content.Id, out var currentWinner))
                {
                    //
                    // The candidate needs the higher authority to take over. Within the same
                    // authority, the higher priority wins, and an equal priority falls back to the
                    // start order, where the plugin processed later wins:
                    //
                    var isTakingOver = authority > currentWinner.Authority || (authority == currentWinner.Authority && plugin.Priority >= currentWinner.Priority);
                    var winnerPluginId = isTakingOver ? content.EnterpriseConfigurationPluginId : currentWinner.Content.EnterpriseConfigurationPluginId;
                    var ignoredPluginId = isTakingOver ? currentWinner.Content.EnterpriseConfigurationPluginId : content.EnterpriseConfigurationPluginId;

                    if (winnerPluginId == ignoredPluginId)
                        LOG.LogWarning($"The configuration plugin '{winnerPluginId}' defines the {contentKind} ID '{content.Id}' more than once. Using its last definition and ignoring the earlier one. Please use each ID only once.");
                    else
                    {
                        var reason = isTakingOver
                            ? DescribeConfigurationPrecedence(authority, plugin.Priority, currentWinner.Authority, currentWinner.Priority)
                            : DescribeConfigurationPrecedence(currentWinner.Authority, currentWinner.Priority, authority, plugin.Priority);

                        LOG.LogWarning($"Multiple configuration plugins define the {contentKind} ID '{content.Id}'. Using the one from the configuration plugin '{winnerPluginId}' and ignoring the one from the configuration plugin '{ignoredPluginId}', because {reason}.");
                    }

                    if (!isTakingOver)
                        continue;
                }

                contentById[content.Id] = (content, authority, plugin.Priority);
            }
        }

        return contentById.Values.Select(entry => entry.Content);
    }

    /// <summary>
    /// Explains in one phrase why one configuration plugin won a collision against another.
    /// </summary>
    /// <remarks>
    /// Administrators read this in the log while they are testing their configuration. Naming the
    /// deciding rule saves them from guessing why their change had no effect.
    /// </remarks>
    private static string DescribeConfigurationPrecedence(int winnerAuthority, int winnerPriority, int ignoredAuthority, int ignoredPriority)
    {
        if (winnerAuthority != ignoredAuthority)
            return "a configuration which acts on behalf of your organization takes precedence over a locally placed one";

        if (winnerPriority != ignoredPriority)
            return $"it declares the higher priority ({winnerPriority} instead of {ignoredPriority})";

        return $"both declare the same priority ({winnerPriority}), so the configuration plugin which started later wins";
    }
}