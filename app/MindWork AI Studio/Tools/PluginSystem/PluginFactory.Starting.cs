using System.Text;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    private static readonly List<PluginBase> RUNNING_PLUGINS = [];
    
    /// <summary>
    /// A list of all running plugins.
    /// </summary>
    public static IReadOnlyCollection<PluginBase> RunningPlugins => RUNNING_PLUGINS;
    
    private static async Task<List<PluginConfigurationObject>> RestartAllPlugins(CancellationToken cancellationToken = default)
    {
        LOG.LogInformation("Try to start or restart all plugins.");
        var configObjects = new List<PluginConfigurationObject>();
        RUNNING_PLUGINS.Clear();

        //
        // Get the base language plugin. This is the plugin that will be used to fill in missing keys.
        //
        var baseLanguagePluginId = InternalPlugin.LANGUAGE_EN_US.MetaData().Id;
        var baseLanguagePluginMetaData = AVAILABLE_PLUGINS.FirstOrDefault(p => p.Id == baseLanguagePluginId);
        if (baseLanguagePluginMetaData is null)
            LOG.LogError($"Was not able to find the base language plugin: Id='{baseLanguagePluginId}'. Please check your installation.");
        else
        {
            try
            {
                var startedBasePlugin = await Start(baseLanguagePluginMetaData, cancellationToken);
                if (startedBasePlugin is NoPlugin noPlugin)
                    LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {noPlugin.Issues.First()}");
        
                if (startedBasePlugin is PluginLanguage languagePlugin)
                {
                    BaseLanguage = languagePlugin;
                    RUNNING_PLUGINS.Add(languagePlugin);
                    LOG.LogInformation($"Successfully started the base language plugin: Id='{languagePlugin.Id}', Type='{languagePlugin.Type}', Name='{languagePlugin.Name}', Version='{languagePlugin.Version}'");
                }
                else
                    LOG.LogError($"Was not able to start the base language plugin: Id='{baseLanguagePluginId}'. Reason: {string.Join("; ", startedBasePlugin.Issues)}");
            }
            catch (Exception e)
            {
                LOG.LogError(e, $"An error occurred while starting the base language plugin: Id='{baseLanguagePluginId}'.");
                BaseLanguage = NoPluginLanguage.INSTANCE;
            }
        }
        
        //
        // Iterate over all available plugins and try to start them. We do that in a deterministic
        // order, starting with the configuration plugins of the organization. Three reasons:
        //
        // - Configuration plugins write settings and configuration objects. Whoever writes one
        //   first owns it, so the organization has to come first: its configuration is the baseline
        //   every other plugin has to respect.
        //
        // - Within one origin, the declared priority decides. An organization can deploy a base
        //   configuration for everybody and refine it, e.g. per department: the higher priority is
        //   applied later and therefore wins.
        //
        // - Without an explicit order, the sequence is the one Directory.EnumerateFiles produced in
        //   LoadAll. That order is not guaranteed, so the same installation could behave
        //   differently on two machines. The plugin directory breaks any remaining tie.
        //
        foreach (var availablePlugin in AVAILABLE_PLUGINS
                     .OrderBy(GetStartupRank)
                     .ThenBy(plugin => plugin.ConfigurationPriority)
                     .ThenBy(plugin => plugin.LocalPath, StringComparer.OrdinalIgnoreCase))
        {
            if(cancellationToken.IsCancellationRequested)
            {
                LOG.LogWarning("Cancellation requested while starting plugins. Stopping the plugin startup process. Probably due to a timeout.");
                break;
            }

            if (availablePlugin.Id == baseLanguagePluginId)
                continue;

            try
            {
                if (availablePlugin.IsInternal || SettingsManagerAccess.IsPluginEnabled(availablePlugin) || availablePlugin.Type == PluginType.CONFIGURATION || availablePlugin.Type == PluginType.ASSISTANT)
                    if(await Start(availablePlugin, cancellationToken) is { IsValid: true } plugin)
                    {
                        if (plugin is PluginConfiguration configPlugin)
                            configObjects.AddRange(configPlugin.ConfigObjects);
                        
                        RUNNING_PLUGINS.Add(plugin);
                    }
            }
            catch (Exception e)
            {
                LOG.LogError(e, $"An error occurred while starting the plugin: Id='{availablePlugin.Id}', Type='{availablePlugin.Type}', Name='{availablePlugin.Name}', Version='{availablePlugin.Version}'.");
            }
        }

        LogAssistantPluginStartupState();
        
        // Inform all components that the plugins have been reloaded or started:
        await MessageBus.INSTANCE.SendMessage<bool>(null, Event.PLUGINS_RELOADED);
        return configObjects;
    }

    /// <summary>
    /// Determines the position of a plugin in the startup sequence. Plugins with a lower rank start earlier.
    /// </summary>
    /// <remarks>
    /// The configuration plugins an organization deployed go first: they are the baseline for
    /// everything else. Local configuration plugins follow, so they can add to that baseline instead
    /// of replacing parts of it. All remaining plugin types write no settings at all, so their rank
    /// is irrelevant for the outcome.<br/><br/>
    /// The rank comes before the declared priority on purpose: a local configuration plugin must not
    /// be able to jump ahead of an organization by declaring a high priority.
    /// </remarks>
    /// <param name="plugin">The plugin about to be started.</param>
    /// <returns>The startup rank of the plugin.</returns>
    private static int GetStartupRank(IAvailablePlugin plugin) => plugin.Type switch
    {
        PluginType.CONFIGURATION when IsEnterpriseConfigurationPath(plugin.LocalPath) => 0,
        PluginType.CONFIGURATION => 1,

        _ => 2,
    };

    private static void LogAssistantPluginStartupState()
    {
        ManagedConfiguration.TryGet(x => x.AssistantPluginAudit, x => x.EnterpriseApprovedPlugins, out ConfigMeta<DataAssistantPluginAudit, IList<DataAssistantPluginEnterpriseApproval>> configMeta);

        foreach (var assistantPlugin in RUNNING_PLUGINS.OfType<PluginAssistants>())
        {
            var securityState = PluginAssistantSecurityResolver.Resolve(SettingsManagerAccess, assistantPlugin);
            if (securityState.IsEnterpriseApproved)
            {
                //
                // Several configuration plugins may approve assistant plugins. We look up the one
                // which approved this particular plugin instead of naming an arbitrary contributor:
                //
                var approvedByConfigPluginId = configMeta.PluginContributions
                    .Where(contribution => contribution.Value.Any(approval => string.Equals(approval.PluginHash, securityState.CurrentHash, StringComparison.Ordinal)))
                    .Select(contribution => contribution.Key)
                    .FirstOrDefault();

                var approvedByConfigPluginName = approvedByConfigPluginId == Guid.Empty
                    ? string.Empty
                    : AVAILABLE_PLUGINS.FirstOrDefault(x => x.Id == approvedByConfigPluginId)?.Name ?? string.Empty;

                LOG.LogInformation(
                    $"Successfully started assistant plugin: Id='{assistantPlugin.Id}', Type='{assistantPlugin.Type}', Name='{assistantPlugin.Name}', Version='{assistantPlugin.Version}', SecuritySource='EnterpriseApproval', ApprovedByConfigPluginId='{approvedByConfigPluginId}', ApprovedByConfigPluginName='{approvedByConfigPluginName}'");
                continue;
            }

            LOG.LogInformation(
                $"Successfully started assistant plugin: Id='{assistantPlugin.Id}', Type='{assistantPlugin.Type}', Name='{assistantPlugin.Name}', Version='{assistantPlugin.Version}'");
        }
    }
    
    private static async Task<PluginBase> Start(IAvailablePlugin meta, CancellationToken cancellationToken = default)
    {
        var pluginMainFile = Path.Join(meta.LocalPath, "plugin.lua");
        if(!File.Exists(pluginMainFile))
        {
            LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reason: The plugin file does not exist.");
            return new NoPlugin($"The plugin file does not exist: {pluginMainFile}");
        }

        var code = await File.ReadAllTextAsync(pluginMainFile, Encoding.UTF8, cancellationToken);
        var plugin = await Load(meta.LocalPath, code, cancellationToken);
        plugin.PluginPath = meta.LocalPath;
        if (plugin is NoPlugin noPlugin)
        {
            LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reason: {noPlugin.Issues.First()}");
            return noPlugin;
        }
        
        if (plugin.IsValid)
        {
            //
            // When this is a language plugin, we need to set the base language plugin.
            //
            if (plugin is PluginLanguage languagePlugin && BaseLanguage != NoPluginLanguage.INSTANCE)
                languagePlugin.SetBaseLanguage(BaseLanguage);
            
            if(plugin is PluginConfiguration configPlugin)
                await configPlugin.InitializeAsync(false);
            
            LOG.LogInformation($"Successfully started plugin: Id='{plugin.Id}', Type='{plugin.Type}', Name='{plugin.Name}', Version='{plugin.Version}'");
            return plugin;
        }

        LOG.LogError($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
        return new NoPlugin($"Was not able to start plugin: Id='{meta.Id}', Type='{meta.Type}', Name='{meta.Name}', Version='{meta.Version}'. Reasons: {string.Join("; ", plugin.Issues)}");
    }
}
