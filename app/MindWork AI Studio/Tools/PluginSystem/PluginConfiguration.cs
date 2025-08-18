using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Chat;

using Lua;

using Host = AIStudio.Provider.SelfHosted.Host;
using Model = AIStudio.Provider.Model;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginConfiguration(bool isInternal, LuaState state, PluginType type) : PluginBase(isInternal, state, type)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginConfiguration).Namespace, nameof(PluginConfiguration));
    private static readonly ILogger<PluginConfiguration> LOGGER = Program.LOGGER_FACTORY.CreateLogger<PluginConfiguration>();
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    
    private readonly List<PluginConfigurationObject> configObjects = [];
    
    /// <summary>
    /// The list of configuration objects. Configuration objects are, e.g., providers or chat templates. 
    /// </summary>
    public IEnumerable<PluginConfigurationObject> ConfigObjects => this.configObjects;
    
    public async Task InitializeAsync(bool dryRun)
    {
        if(!this.TryProcessConfiguration(dryRun, out var issue))
            this.pluginIssues.Add(issue);

        if (!dryRun)
        {
            await SETTINGS_MANAGER.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Tries to initialize the UI text content of the plugin.
    /// </summary>
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="message">The error message, when the UI text content could not be read.</param>
    /// <returns>True, when the UI text content could be read successfully.</returns>
    private bool TryProcessConfiguration(bool dryRun, out string message)
    {
        this.configObjects.Clear();
        
        // Ensure that the main CONFIG table exists and is a valid Lua table:
        if (!this.state.Environment["CONFIG"].TryRead<LuaTable>(out var mainTable))
        {
            message = TB("The CONFIG table does not exist or is not a valid table.");
            return false;
        }
        
        //
        // ===========================================
        // Configured settings
        // ===========================================
        //
        if (!mainTable.TryGetValue("SETTINGS", out var settingsValue) || !settingsValue.TryRead<LuaTable>(out var settingsTable))
        {
            message = TB("The SETTINGS table does not exist or is not a valid table.");
            return false;
        }
        
        // Check for updates, and if so, how often?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateBehavior, this.Id, settingsTable, dryRun);
        
        // Allow the user to add providers?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToAddProvider, this.Id, settingsTable, dryRun);

        //
        // Configured providers:
        //
        if (!mainTable.TryGetValue("LLM_PROVIDERS", out var providersValue) || !providersValue.TryRead<LuaTable>(out var providersTable))
        {
            message = TB("The LLM_PROVIDERS table does not exist or is not a valid table.");
            return false;
        }

        message = string.Empty;
        var numberProviders = providersTable.ArrayLength;
        var configuredProviders = new List<Settings.Provider>(numberProviders);
        for (var i = 1; i <= numberProviders; i++)
        {
            var providerLuaTableValue = providersTable[i];
            if (!providerLuaTableValue.TryRead<LuaTable>(out var providerLuaTable))
            {
                LOGGER.LogWarning($"The LLM_PROVIDERS table at index {i} is not a valid table.");
                continue;
            }
            
            if(this.TryReadProviderTable(i, providerLuaTable, out var provider))
                configuredProviders.Add(provider);
            else
                LOGGER.LogWarning($"The LLM_PROVIDERS table at index {i} does not contain a valid provider configuration.");
        }
        
        //
        // Apply the configured providers to the system settings:
        //
        #pragma warning disable MWAIS0001
        foreach (var configuredProvider in configuredProviders)
        {
            // The iterating variable is immutable, so we need to create a local copy:
            var provider = configuredProvider;

            // Store this provider in the config object list:
            this.configObjects.Add(new()
            {
                ConfigPluginId = this.Id,
                Id = Guid.Parse(provider.Id),
                Type = PluginConfigurationObjectType.LLM_PROVIDER,
            });

            if (dryRun)
                continue;
            
            var providerIndex = SETTINGS_MANAGER.ConfigurationData.Providers.FindIndex(p => p.Id == provider.Id);
            if (providerIndex > -1)
            {
                // Case: The provider already exists, we update it:
                var existingProvider = SETTINGS_MANAGER.ConfigurationData.Providers[providerIndex];
                provider = provider with { Num = existingProvider.Num }; // Keep the original number
                SETTINGS_MANAGER.ConfigurationData.Providers[providerIndex] = provider;
            }
            else
            {
                // Case: The provider does not exist, we add it:
                provider = provider with { Num = SETTINGS_MANAGER.ConfigurationData.NextProviderNum++ };
                SETTINGS_MANAGER.ConfigurationData.Providers.Add(provider);
            }
        }
        
        #pragma warning restore MWAIS0001
        
        //
        // Configured chat templates:
        //
        if (mainTable.TryGetValue("CHAT_TEMPLATES", out var templatesValue) && templatesValue.TryRead<LuaTable>(out var templatesTable))
        {
            var numberTemplates = templatesTable.ArrayLength;
            var configuredTemplates = new List<ChatTemplate>(numberTemplates);
            for (var i = 1; i <= numberTemplates; i++)
            {
                var templateLuaTableValue = templatesTable[i];
                if (!templateLuaTableValue.TryRead<LuaTable>(out var templateLuaTable))
                {
                    LOGGER.LogWarning($"The CHAT_TEMPLATES table at index {i} is not a valid table.");
                    continue;
                }
                
                if(this.TryReadChatTemplateTable(i, templateLuaTable, out var template) && template != ChatTemplate.NO_CHAT_TEMPLATE)
                    configuredTemplates.Add(template);
                else
                    LOGGER.LogWarning($"The CHAT_TEMPLATES table at index {i} does not contain a valid chat template configuration.");
            }
            
            // Apply configured chat templates to the system settings:
            foreach (var configuredTemplate in configuredTemplates)
            {
                // The iterating variable is immutable, so we need to create a local copy:
                var template = configuredTemplate;
                
                // Store this provider in the config object list:
                this.configObjects.Add(new()
                {
                    ConfigPluginId = this.Id,
                    Id = Guid.Parse(template.Id),
                    Type = PluginConfigurationObjectType.CHAT_TEMPLATE,
                });

                if (dryRun)
                    continue;
                
                var tplIndex = SETTINGS_MANAGER.ConfigurationData.ChatTemplates.FindIndex(t => t.Id == template.Id);
                if (tplIndex > -1)
                {
                    // Case: The template already exists, we update it:
                    var existingTemplate = SETTINGS_MANAGER.ConfigurationData.ChatTemplates[tplIndex];
                    template = template with { Num = existingTemplate.Num };
                    SETTINGS_MANAGER.ConfigurationData.ChatTemplates[tplIndex] = template;
                }
                else
                {
                    // Case: The template does not exist, we add it:
                    template = template with { Num = SETTINGS_MANAGER.ConfigurationData.NextChatTemplateNum++ };
                    SETTINGS_MANAGER.ConfigurationData.ChatTemplates.Add(template);
                }
            }
        }
        
        return true;
    }
    
    private bool TryReadProviderTable(int idx, LuaTable table, out Settings.Provider provider)
    {
        provider = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }

        if (!table.TryGetValue("InstanceName", out var instanceNameValue) || !instanceNameValue.TryRead<string>(out var instanceName))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid instance name.");
            return false;
        }

        if (!table.TryGetValue("UsedLLMProvider", out var usedLLMProviderValue) || !usedLLMProviderValue.TryRead<string>(out var usedLLMProviderText) || !Enum.TryParse<LLMProviders>(usedLLMProviderText, true, out var usedLLMProvider))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid LLM provider enum value.");
            return false;
        }
        
        if (!table.TryGetValue("Host", out var hostValue) || !hostValue.TryRead<string>(out var hostText) || !Enum.TryParse<Host>(hostText, true, out var host))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid host enum value.");
            return false;
        }
        
        if (!table.TryGetValue("Hostname", out var hostnameValue) || !hostnameValue.TryRead<string>(out var hostname))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid hostname.");
            return false;
        }
        
        if (!table.TryGetValue("Model", out var modelValue) || !modelValue.TryRead<LuaTable>(out var modelTable))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model table.");
            return false;
        }
        
        if (!this.TryReadModelTable(idx, modelTable, out var model))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model configuration.");
            return false;
        }

        provider = new()
        {
            Num = 0,
            Id = id.ToString(),
            InstanceName = instanceName,
            UsedLLMProvider = usedLLMProvider,
            Model = model,
            IsSelfHosted = usedLLMProvider is LLMProviders.SELF_HOSTED,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = this.Id,
            Hostname = hostname,
            Host = host
        };
        
        return true;
    }

    private bool TryReadModelTable(int idx, LuaTable table, out Model model)
    {
        model = default;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var id))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model ID.");
            return false;
        }
        
        if (!table.TryGetValue("DisplayName", out var displayNameValue) || !displayNameValue.TryRead<string>(out var displayName))
        {
            LOGGER.LogWarning($"The configured provider {idx} does not contain a valid model display name.");
            return false;
        }
        
        model = new(id, displayName);
        return true;
    }

    private bool TryReadChatTemplateTable(int idx, LuaTable table, out ChatTemplate template)
    {
        template = ChatTemplate.NO_CHAT_TEMPLATE;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid ID. The ID must be a valid GUID.");
            return false;
        }
        
        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid name.");
            return false;
        }
        
        if (!table.TryGetValue("SystemPrompt", out var sysPromptValue) || !sysPromptValue.TryRead<string>(out var systemPrompt))
        {
            LOGGER.LogWarning($"The configured chat template {idx} does not contain a valid system prompt.");
            return false;
        }
        
        var predefinedUserPrompt = string.Empty;
        if (table.TryGetValue("PredefinedUserPrompt", out var preUserValue) && preUserValue.TryRead<string>(out var preUser))
            predefinedUserPrompt = preUser;
        
        var allowProfileUsage = false;
        if (table.TryGetValue("AllowProfileUsage", out var allowProfileValue) && allowProfileValue.TryRead<bool>(out var allow))
            allowProfileUsage = allow;
        
        template = new()
        {
            Num = 0,
            Id = id.ToString(),
            Name = name,
            SystemPrompt = systemPrompt,
            PredefinedUserPrompt = predefinedUserPrompt,
            ExampleConversation = ParseExampleConversation(idx, table),
            AllowProfileUsage = allowProfileUsage,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = this.Id
        };
        
        return true;
    }

    private static List<ContentBlock> ParseExampleConversation(int idx, LuaTable table)
    {
        var exampleConversation = new List<ContentBlock>();
        if (!table.TryGetValue("ExampleConversation", out var exConvValue) || !exConvValue.TryRead<LuaTable>(out var exConvTable))
            return exampleConversation;
        
        var numBlocks = exConvTable.ArrayLength;
        for (var j = 1; j <= numBlocks; j++)
        {
            var blockValue = exConvTable[j];
            if (!blockValue.TryRead<LuaTable>(out var blockTable))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} is not a valid table.");
                continue;
            }
            
            if (!blockTable.TryGetValue("Role", out var roleValue) || !roleValue.TryRead<string>(out var roleText) || !Enum.TryParse<ChatRole>(roleText, true, out var parsedRole))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid role.");
                continue;
            }

            if (!blockTable.TryGetValue("Content", out var contentValue) || !contentValue.TryRead<string>(out var content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} does not contain a valid content message.");
                continue;
            }
                
            if (string.IsNullOrWhiteSpace(content))
            {
                LOGGER.LogWarning($"The ExampleConversation entry {j} in chat template {idx} contains an empty content message.");
                continue;
            }
                
            exampleConversation.Add(new ContentBlock
            {
                Time = DateTimeOffset.UtcNow,
                Role = parsedRole,
                Content = new ContentText { Text = content },
                ContentType = ContentType.TEXT,
                HideFromUser = true,
            });
        }

        return exampleConversation;
    }
}