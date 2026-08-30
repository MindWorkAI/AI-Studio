using System.Linq.Expressions;
using System.Text;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem.Assistants;
using Lua;
using Lua.Standard;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly List<IAvailablePlugin> AVAILABLE_PLUGINS = [];
    private static readonly SemaphoreSlim PLUGIN_LOAD_SEMAPHORE = new(1, 1);
    
    /// <summary>
    /// A list of all available plugins.
    /// </summary>
    public static IReadOnlyCollection<IPluginMetadata> AvailablePlugins => AVAILABLE_PLUGINS;
    
    /// <summary>
    /// Try to load all plugins from the plugins directory.
    /// </summary>
    /// <remarks>
    /// Loading plugins means:<br/>
    /// - Parsing and checking the plugin code<br/>
    /// - Check for forbidden plugins<br/>
    /// - Creating a new instance of the allowed plugin<br/>
    /// - Read the plugin metadata<br/>
    /// - Start the plugin<br/>
    /// </remarks>
    public static async Task LoadAll(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }
        
        // Wait for ongoing reloads instead of silently skipping this request.
        // This caller must return only after its reload has run.
        await PLUGIN_LOAD_SEMAPHORE.WaitAsync(cancellationToken);

        var configObjectList = new List<PluginConfigurationObject>();
        
        try
        {
            LOG.LogInformation("Start loading plugins.");

            //
            // Without the plugins directory, we cannot load or start any plugin. Still, we must not
            // stop here: the clean-up at the end of this method has to run. Otherwise, settings which
            // a configuration plugin has locked would stay locked forever.
            //
            var pluginsDirectoryExists = Directory.Exists(PLUGINS_ROOT);
            if (!pluginsDirectoryExists)
                LOG.LogWarning("No plugins found. Checking for left-over configurations of removed configuration plugins.");

            AVAILABLE_PLUGINS.Clear();

            //
            // The easiest way to load all plugins is to find all `plugin.lua` files and load them.
            // By convention, each plugin is enforced to have a `plugin.lua` file.
            //
            IEnumerable<string> pluginMainFiles = pluginsDirectoryExists ? Directory.EnumerateFiles(PLUGINS_ROOT, "plugin.lua", SearchOption.AllDirectories) : [];
            foreach (var pluginMainFile in pluginMainFiles)
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        LOG.LogWarning("Was not able to load all plugins, because the operation was cancelled. It seems to be a timeout.");
                        break;
                    }

                    LOG.LogInformation($"Try to load plugin: {pluginMainFile}");
                    var fileInfo = new FileInfo(pluginMainFile);
                    string code;
                    await using(var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using var reader = new StreamReader(fileStream, Encoding.UTF8);
                        code = await reader.ReadToEndAsync(cancellationToken);
                    }
                    
                    var pluginPath = Path.GetDirectoryName(pluginMainFile)!;
                    var plugin = await Load(pluginPath, code, cancellationToken);
            
                    switch (plugin)
                    {
                        case NoPlugin noPlugin when noPlugin.Issues.Any():
                            LOG.LogError($"Was not able to load plugin: '{pluginMainFile}'. Reason: {noPlugin.Issues.First()}");
                            continue;
                
                        case NoPlugin:
                            LOG.LogError($"Was not able to load plugin: '{pluginMainFile}'. Reason: Unknown.");
                            continue;
                
                        case { IsValid: false }:
                            LOG.LogError($"Was not able to load plugin '{pluginMainFile}', because the Lua code is not a valid AI Studio plugin. There are {plugin.Issues.Count()} issues to fix. First issue is: {plugin.Issues.FirstOrDefault()}");
#if DEBUG
                            foreach (var pluginIssue in plugin.Issues)
                                LOG.LogError($"Plugin issue: {pluginIssue}");
#endif
                            continue;

                        case { IsMaintained: false }:
                            LOG.LogWarning($"The plugin '{pluginMainFile}' is not maintained anymore. Please consider to disable it.");
                            break;
                    }
            
                    LOG.LogInformation($"Successfully loaded plugin: '{pluginMainFile}' (Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}', Authors='{string.Join(", ", plugin.Authors)}')");

                    //
                    // Plugin IDs must be unique: many lookups resolve a plugin by its ID alone, e.g.
                    // the base language plugin in PluginFactory.Starting or the owner of a locked
                    // setting. When two plugins share an ID, the one deployed by the organization's
                    // IT wins. Otherwise, a manually placed copy could outrank the enterprise
                    // configuration, which is the exact opposite of what an organization expects:
                    //
                    if (AVAILABLE_PLUGINS.FirstOrDefault(candidate => candidate.Id == plugin.Id) is { } duplicatePlugin)
                    {
                        if (GetConfigurationAuthority(pluginPath) <= GetConfigurationAuthority(duplicatePlugin.LocalPath))
                        {
                            LOG.LogWarning($"Ignoring the plugin '{pluginMainFile}': its ID ('{plugin.Id}') is already used by the plugin at '{duplicatePlugin.LocalPath}'. Plugin IDs must be unique. Please remove one of these plugins.");
                            continue;
                        }

                        if (IsEnterpriseTestConfigurationPath(pluginPath))
                            LOG.LogWarning($"Ignoring the plugin at '{duplicatePlugin.LocalPath}': it uses the ID ('{plugin.Id}') of the test configuration plugin at '{pluginPath}'. A test configuration takes precedence until AI Studio is restarted.");
                        else
                            LOG.LogWarning($"Ignoring the plugin at '{duplicatePlugin.LocalPath}': it uses the ID ('{plugin.Id}') of the enterprise configuration plugin at '{pluginPath}'. Plugins deployed by your organization's IT take precedence.");

                        AVAILABLE_PLUGINS.Remove(duplicatePlugin);
                    }

                    //
                    // An organization may deploy any kind of plugin, not just configurations: the
                    // archive it serves under a configuration ID often carries an assistant plugin
                    // in a subdirectory as well. Everything stored below one of the organization's
                    // directories therefore belongs to that organization, whatever its type is and
                    // however deeply it is nested:
                    //
                    var isInOrganizationDirectory = IsOrganizationConfigurationPath(pluginPath);

                    Guid? managedConfigurationId = null;
                    var configurationPriority = 0;
                    bool? declaredAsManagedByConfigServer = null;
                    if (plugin is PluginConfiguration configPlugin)
                    {
                        configurationPriority = configPlugin.Priority;
                        declaredAsManagedByConfigServer = configPlugin.DeployedUsingConfigServer;
                    }
                    else if (plugin is PluginAssistants { HasDeploymentManagementMetadata: true } assistantPlugin)
                        declaredAsManagedByConfigServer = assistantPlugin.IsManagedByConfigServer;

                    //
                    // The plugin path outranks what a plugin declares about itself. A plugin an
                    // organization deployed could otherwise deny it and escape the withdrawal of that
                    // configuration, while keeping every right the directory grants it:
                    //
                    var isManagedByConfigServer = isInOrganizationDirectory || declaredAsManagedByConfigServer is true;
                    switch (declaredAsManagedByConfigServer)
                    {
                        case null when isInOrganizationDirectory:
                            LOG.LogWarning($"The {plugin.Type} plugin '{plugin.Id}' does not define 'DEPLOYED_USING_CONFIG_SERVER'. Falling back to the plugin path and treating it as managed because it is stored under '{pluginPath}'.");
                            break;

                        case false when isInOrganizationDirectory:
                            LOG.LogWarning($"The {plugin.Type} plugin '{plugin.Id}' declares 'DEPLOYED_USING_CONFIG_SERVER = false', but it is stored under '{pluginPath}' and therefore belongs to your organization. Treating it as managed. Please fix the plugin.");
                            break;
                    }

                    //
                    // Which configuration a plugin was deployed with is what ties it to the archive it
                    // came from. Only the configuration plugin itself must carry the configuration ID
                    // as its own ID: a plugin deployed alongside it has an ID of its own:
                    //
                    if (IsEnterpriseConfigurationPath(pluginPath))
                    {
                        if (TryGetDeployedConfigurationId(pluginPath, out var enterpriseConfigId))
                        {
                            managedConfigurationId = enterpriseConfigId;
                            if (plugin.Type is PluginType.CONFIGURATION && enterpriseConfigId != plugin.Id)
                                LOG.LogWarning($"The configuration plugin's ID ('{plugin.Id}') does not match the enterprise configuration ID ('{enterpriseConfigId}'). These IDs should be identical. Please update the plugin's ID field to match the enterprise configuration ID.");
                        }
                        else
                            LOG.LogWarning($"Could not determine the managed configuration ID for the {plugin.Type} plugin '{plugin.Id}'. The plugin directory '{pluginPath}' is not nested in a directory named after a configuration ID.");
                    }

                    AVAILABLE_PLUGINS.Add(new PluginMetadata(plugin, pluginPath, isManagedByConfigServer, managedConfigurationId, configurationPriority));
                }
                catch (Exception e)
                {
                    LOG.LogError($"Was not able to load plugin '{pluginMainFile}'. Issue: {e.Message}");
                    LOG.LogDebug(e.StackTrace);
                }
            }
        
            // Start or restart all plugins:
            if (pluginsDirectoryExists)
            {
                var configObjects = await RestartAllPlugins(cancellationToken);
                configObjectList.AddRange(configObjects);
            }
        }
        finally
        {
            PLUGIN_LOAD_SEMAPHORE.Release();
            LOG.LogInformation("Finished loading plugins.");
        }
        
        //
        // =========================================================
        // Next, we have to clean up our settings. It is possible
        // that a configuration plugin was removed. We have to
        // remove the related settings as well:
        // =========================================================
        //
        
        //
        // Enterprise configuration plugins which are deployed but could not be loaded count as
        // present: they were not removed, so everything they manage must stay as it is. Otherwise,
        // one broken configuration plugin would wipe the entire organization configuration:
        //
        var deployedEnterpriseConfigPluginIds = GetDeployedEnterpriseConfigPluginIds();

        //
        // Test configurations manage settings and objects like a deployed configuration, so those must
        // not be treated as left over while the test runs. They are only ever loaded, never merely
        // present: the test directory is emptied on every start.
        //
        foreach (var testConfigurationPlugin in AVAILABLE_PLUGINS.Where(plugin => plugin.Type is PluginType.CONFIGURATION && IsEnterpriseTestConfigurationPath(plugin.LocalPath)))
            deployedEnterpriseConfigPluginIds.Add(testConfigurationPlugin.Id);

        //
        // A deployment does not have to contain a configuration plugin under its own ID: an
        // organization uses the same channel to roll out assistant plugins and other plugin types.
        // We therefore collect which deployments contributed a plugin at all, so that such a rollout
        // is not mistaken for a configuration nobody could read:
        //
        var configurationIdsWithLoadedPlugins = AVAILABLE_PLUGINS
            .Where(plugin => plugin.ManagedConfigurationId.HasValue)
            .Select(plugin => plugin.ManagedConfigurationId!.Value)
            .ToHashSet();

        var unloadedEnterpriseConfigPluginIds = deployedEnterpriseConfigPluginIds.Where(x => AVAILABLE_PLUGINS.All(plugin => plugin.Id != x)).ToList();
        foreach (var unloadedEnterpriseConfigPluginId in unloadedEnterpriseConfigPluginIds)
        {
            if (configurationIdsWithLoadedPlugins.Contains(unloadedEnterpriseConfigPluginId))
            {
                LOG.LogInformation($"The deployment '{unloadedEnterpriseConfigPluginId}' contains no configuration plugin of its own, but other plugins your organization deployed with it were loaded. Should you expect a configuration plugin here, please check the errors above.");
                continue;
            }

            LOG.LogWarning($"The configuration plugin '{unloadedEnterpriseConfigPluginId}' is deployed, but was not loaded. Everything it manages stays unchanged, because the plugin was not removed. Please check the errors above and fix the plugin.");
        }

        // Check LLM providers:
        var wasConfigurationChanged = await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList, SecretStoreType.LLM_PROVIDER);

        // Check transcription providers:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER, x => x.TranscriptionProviders, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList, SecretStoreType.TRANSCRIPTION_PROVIDER))
            wasConfigurationChanged = true;

        // Check embedding providers:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.EMBEDDING_PROVIDER, x => x.EmbeddingProviders, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList, SecretStoreType.EMBEDDING_PROVIDER))
            wasConfigurationChanged = true;

        // Check data sources:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.DATA_SOURCE, x => x.DataSources, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList, SecretStoreType.DATA_SOURCE, deleteSecret: true))
            wasConfigurationChanged = true;

        // Check chat templates:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList))
            wasConfigurationChanged = true;

        // Check profiles:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.PROFILE, x => x.Profiles, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList))
            wasConfigurationChanged = true;

        // Check document analysis policies:
        if(await PluginConfigurationObject.CleanLeftOverConfigurationObjects(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY, x => x.DocumentAnalysis.Policies, AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds, configObjectList))
            wasConfigurationChanged = true;

        // Check left-over mandatory info acceptances:
        if (SettingsManagerAccess.ConfigurationData.MandatoryInformation.RemoveLeftOverAcceptances(GetMandatoryInfos()))
            wasConfigurationChanged = true;
        
        // Check all managed settings, i.e. settings which a configuration plugin can lock,
        // provide as an editable default, or contribute to:
        if(ManagedConfiguration.CleanupLeftOverManagedConfigurations(AVAILABLE_PLUGINS, deployedEnterpriseConfigPluginIds))
            wasConfigurationChanged = true;

        //
        // The enterprise approvals of all configuration plugins add up. Now that every plugin has
        // contributed and the clean-up above has dropped the removed ones, we rebuild the effective
        // list. We skip that while a configuration plugin is deployed but could not be loaded: its
        // approvals are missing from the contributions, and withdrawing them would demand a new
        // security audit for assistant plugins the organization has approved:
        //
        if(unloadedEnterpriseConfigPluginIds.Count == 0 && PluginConfiguration.RefreshEnterpriseApprovedAssistantPlugins())
            wasConfigurationChanged = true;

        //
        // Now that the approvals are final, we know which assistant plugins your organization wants
        // enabled. This needs no guard of its own: it reads the stored approvals, which stay in place
        // when a configuration plugin could not be loaded:
        //
        if(RefreshEnterpriseAssistantActivations())
            wasConfigurationChanged = true;

        // Compatibility shim, see documentation/compatibility-shims/2026-08-orphaned-config-locks.md (remove after 2027-08-06):
        if (RepairLegacyConfigOnlySettings(unloadedEnterpriseConfigPluginIds.Count > 0))
            wasConfigurationChanged = true;

        if (wasConfigurationChanged)
        {
            await SettingsManagerAccess.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Determines the IDs of all configuration plugins which an organization deployed on this machine.
    /// </summary>
    /// <remarks>
    /// Local configuration plugins are not part of this: they belong to the user, not to an
    /// organization, and they can live in any directory below the plugins root.<br/><br/>
    /// We read these IDs from the file system instead of taking them from the loaded plugins. A
    /// configuration plugin might be present but not loadable, e.g. due to invalid Lua code, a
    /// missing `plugin.lua`, or an incomplete download. Such a plugin still manages this AI Studio
    /// instance, so we must not treat its settings as left over. Configuration plugins deployed by a
    /// configuration server live in a directory named after their ID, which is the only information
    /// left when the plugin itself cannot be read.
    /// </remarks>
    private static HashSet<Guid> GetDeployedEnterpriseConfigPluginIds()
    {
        var deployedEnterpriseConfigPluginIds = new HashSet<Guid>();
        if (!Directory.Exists(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT))
            return deployedEnterpriseConfigPluginIds;

        foreach (var configPluginDirectory in Directory.EnumerateDirectories(ENTERPRISE_CONFIGURATION_PLUGINS_ROOT))
        {
            if (!Guid.TryParse(Path.GetFileName(configPluginDirectory), out var configPluginId) || configPluginId == Guid.Empty)
                continue;

            // An empty directory is a left-over of a removed plugin, not a deployed plugin:
            if (!Directory.EnumerateFileSystemEntries(configPluginDirectory).Any())
                continue;

            deployedEnterpriseConfigPluginIds.Add(configPluginId);
        }

        return deployedEnterpriseConfigPluginIds;
    }

    /// <param name="pluginPath">The directory the plugin is located in, or null when the code has no directory yet.</param>
    /// <param name="code">The Lua code of the plugin's main file.</param>
    /// <param name="cancellationToken">Cancellation token for running the Lua code.</param>
    /// <param name="allowedBaseDirectory">
    /// The directory the plugin path must be nested in. Without it, the installed plugins directory
    /// is used. Validating a plugin before its installation needs this, because the plugin lives in
    /// a staging directory at that point and could not load any of its own Lua modules otherwise.
    /// </param>
    public static async Task<PluginBase> Load(string? pluginPath, string code, CancellationToken cancellationToken = default, string? allowedBaseDirectory = null)
    {
        if(ForbiddenPlugins.Check(code) is { IsForbidden: true } forbiddenState)
            return new NoPlugin($"This plugin is forbidden: {forbiddenState.Message}");

        var state = LuaState.Create();
        if (!string.IsNullOrWhiteSpace(pluginPath))
        {
            // Add the module loader so that the plugin can load other Lua modules:
            state.ModuleLoader = new PluginLoader(pluginPath, allowedBaseDirectory);
        }

        // Add some useful libraries:
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        state.OpenStringLibrary();
        state.OpenTableLibrary();
        state.OpenMathLibrary();
        state.OpenBitwiseLibrary();
        state.OpenCoroutineLibrary();

        try
        {
            await state.DoStringAsync(code, cancellationToken: cancellationToken);
        }
        catch (LuaParseException e)
        {
            return new NoPlugin($"Was not able to parse the plugin: {e.Message}");
        }
        catch (LuaRuntimeException e)
        {
            return new NoPlugin($"Was not able to run the plugin: {e.Message}");
        }
        
        if (!state.Environment["TYPE"].TryRead<string>(out var typeText))
            return new NoPlugin("TYPE does not exist or is not a valid string.");
        
        if (!Enum.TryParse<PluginType>(typeText, out var type))
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        if(type is PluginType.NONE)
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        // Whether a plugin is internal is decided by its path, never by the plugin itself. We use the
        // same nesting check as everywhere else, so that a directory like `.internal-old` next to the
        // internal plugins does not count as internal:
        var isInternal = IsPathInside(INTERNAL_PLUGINS_ROOT, pluginPath);
        switch (type)
        {
            case PluginType.LANGUAGE:
                return new PluginLanguage(isInternal, state, type);
            
            case PluginType.CONFIGURATION:
                var configPlug = new PluginConfiguration(isInternal, state, type)
                {
                    PluginPath = pluginPath ?? string.Empty
                };
                
                await configPlug.InitializeAsync(true);
                return configPlug;
            
            case PluginType.ASSISTANT:
                var assistantPlugin = new PluginAssistants(isInternal, state, type);
                assistantPlugin.TryLoad();
                return assistantPlugin;
            
            default:
                return new NoPlugin("This plugin type is not supported yet. Please try again with a future version of AI Studio.");
        }
    }

    //
    // =========================================================
    // Compatibility shim. Please read the related document
    // before you change anything here:
    //
    //   documentation/compatibility-shims/2026-08-orphaned-config-locks.md
    //
    // Remove after 2027-08-06. Everything from here down to the
    // end of this file belongs to the shim and can be deleted
    // in one piece.
    // =========================================================
    //

    /// <summary>
    /// Repairs settings that were configured by a configuration plugin which was removed before
    /// AI Studio started to persist the configuration ownership.
    /// </summary>
    /// <remarks>
    /// All settings listed here share two properties: a configuration plugin can set them, and
    /// there is no user interface to change them back. Therefore, any value that differs from the
    /// default must originate from a configuration plugin. When such a setting is not managed
    /// anymore, its plugin is gone and we restore the default value.<br/><br/>
    /// This is only valid as long as none of these settings gets a user interface. When you add
    /// one, remove the setting from this method and from the shim's document.
    /// </remarks>
    /// <param name="hasUnloadedConfigPlugins" >
    /// True when at least one configuration plugin is deployed but could not be loaded. In that case,
    /// we cannot tell whether a value comes from that plugin or from a removed one, so we repair
    /// nothing at all.
    /// </param>
    /// <returns>True when at least one setting was repaired, otherwise false.</returns>
    private static bool RepairLegacyConfigOnlySettings(bool hasUnloadedConfigPlugins)
    {
        if (hasUnloadedConfigPlugins)
        {
            LOG.LogWarning("Skipping the repair of configuration-only settings: at least one configuration plugin is deployed, but could not be loaded. We try again the next time AI Studio starts.");
            return false;
        }

        var data = SettingsManagerAccess.ConfigurationData;
        var wasRepaired = false;

        // Settings which are enabled by default and which only a configuration plugin can switch off:
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.ShowIntroduction, data.App.ShowIntroduction);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.ShowQuickStartGuide, data.App.ShowQuickStartGuide);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.ShowLastChangelog, data.App.ShowLastChangelog);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.ShowVision, data.App.ShowVision);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.AllowUserToAddProvider, data.App.AllowUserToAddProvider);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.AllowUserToImportPlugins, data.App.AllowUserToImportPlugins);
        wasRepaired |= RepairLegacyConfigOnlyFlag(x => x.App, x => x.AllowUserToSharePlugins, data.App.AllowUserToSharePlugins);

        // Collections which stay empty unless a configuration plugin fills them:
        wasRepaired |= RepairLegacyConfigOnlyCollection(x => x.App, x => x.HiddenAssistants, data.App.HiddenAssistants.Count);
        wasRepaired |= RepairLegacyConfigOnlyCollection(x => x.DataSourceSecurity, x => x.TrustedProviderIds, data.DataSourceSecurity.TrustedProviderIds.Count);
        wasRepaired |= RepairLegacyConfigOnlyCollection(x => x.AssistantPluginAudit, x => x.EnterpriseApprovedPlugins, data.AssistantPluginAudit.EnterpriseApprovedPlugins.Count);

        return wasRepaired;
    }

    /// <summary>
    /// Restores the default of a boolean setting when it is switched off without being managed.
    /// </summary>
    private static bool RepairLegacyConfigOnlyFlag<TClass>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, bool>> propertyExpression, bool currentValue)
    {
        if (currentValue)
            return false;

        if (!ManagedConfiguration.TryGet(configSelection, propertyExpression, out var configMeta) || configMeta.ManagedMode is not null)
            return false;

        LOG.LogWarning($"Repairing the setting '{configMeta.SettingName}': it was switched off by a configuration plugin which is not available anymore.");
        configMeta.ResetLockedConfiguration();
        return true;
    }

    /// <summary>
    /// Clears a set-based setting when it contains entries without being managed.
    /// </summary>
    private static bool RepairLegacyConfigOnlyCollection<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, ISet<TValue>>> propertyExpression, int currentCount)
    {
        if (currentCount is 0)
            return false;

        if (!ManagedConfiguration.TryGet(configSelection, propertyExpression, out var configMeta) || configMeta.ManagedMode is not null)
            return false;

        LOG.LogWarning($"Repairing the setting '{configMeta.SettingName}': it was filled by a configuration plugin which is not available anymore.");
        configMeta.ResetLockedConfiguration();
        return true;
    }

    /// <summary>
    /// Clears a list-based setting when it contains entries without being managed.
    /// </summary>
    private static bool RepairLegacyConfigOnlyCollection<TClass, TValue>(Expression<Func<Data, TClass>> configSelection, Expression<Func<TClass, IList<TValue>>> propertyExpression, int currentCount)
    {
        if (currentCount is 0)
            return false;

        if (!ManagedConfiguration.TryGet(configSelection, propertyExpression, out var configMeta) || configMeta.ManagedMode is not null)
            return false;

        LOG.LogWarning($"Repairing the setting '{configMeta.SettingName}': it was filled by a configuration plugin which is not available anymore.");
        configMeta.ResetLockedConfiguration();
        return true;
    }
}
