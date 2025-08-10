using System.Text.Json;

using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings;

public static class SettingsMigrations
{
    public static Data Migrate(ILogger<SettingsManager> logger, Version previousVersion, string configData, JsonSerializerOptions jsonOptions)
    {
        switch (previousVersion)
        {
            case Version.V1:
                var configV1 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV1 is null)
                {
                    logger.LogError("Failed to parse the v1 configuration. Using default values.");
                    return new();
                }

                configV1 = MigrateV1ToV2(logger, configV1);
                configV1 = MigrateV2ToV3(logger, configV1);
                var configV14 = MigrateV3ToV4(logger, configV1);
                return MigrateV4ToV5(logger, configV14);

            case Version.V2:
                var configV2 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV2 is null)
                {
                    logger.LogError("Failed to parse the v2 configuration. Using default values.");
                    return new();
                }

                configV2 = MigrateV2ToV3(logger, configV2);
                var configV24 = MigrateV3ToV4(logger, configV2);
                return MigrateV4ToV5(logger, configV24);

            case Version.V3:
                var configV3 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV3 is null)
                {
                    logger.LogError("Failed to parse the v3 configuration. Using default values.");
                    return new();
                }

                var configV34 = MigrateV3ToV4(logger, configV3);
                return MigrateV4ToV5(logger, configV34);
            
            case Version.V4:
                var configV4 = JsonSerializer.Deserialize<DataV4>(configData, jsonOptions);
                if (configV4 is null)
                {
                    logger.LogError("Failed to parse the v4 configuration. Using default values.");
                    return new();
                }

                return MigrateV4ToV5(logger, configV4);

            default:
                logger.LogInformation("No configuration migration is needed.");
                var configV5 = JsonSerializer.Deserialize<Data>(configData, jsonOptions);
                if (configV5 is null)
                {
                    logger.LogError("Failed to parse the v4 configuration. Using default values.");
                    return new();
                }

                return configV5;
        }
    }

    private static DataV1V3 MigrateV1ToV2(ILogger<SettingsManager> logger, DataV1V3 previousData)
    {
        //
        // Summary:
        // In v1 we had no self-hosted providers. Thus, we had no hostnames.
        //

        logger.LogInformation("Migrating from v1 to v2...");
        return new()
        {
            Version = Version.V2,
            
            Providers = previousData.Providers.Select(provider => provider with { IsSelfHosted = false, Hostname = string.Empty }).ToList(),
            
            EnableSpellchecking = previousData.EnableSpellchecking,
            IsSavingEnergy = previousData.IsSavingEnergy,
            NextProviderNum = previousData.NextProviderNum,
            ShortcutSendBehavior = previousData.ShortcutSendBehavior,
            UpdateBehavior = previousData.UpdateBehavior,
        };
    }
    
    private static DataV1V3 MigrateV2ToV3(ILogger<SettingsManager> logger, DataV1V3 previousData)
    {
        //
        // Summary:
        // In v2, self-hosted providers had no host (LM Studio, llama.cpp, ollama, etc.)
        //

        logger.LogInformation("Migrating from v2 to v3...");
        return new()
        {
            Version = Version.V3,
            Providers = previousData.Providers.Select(provider =>
            {
                if(provider.IsSelfHosted)
                    return provider with { Host = Host.LM_STUDIO };
                
                return provider with { Host = Host.NONE };
            }).ToList(),

            EnableSpellchecking = previousData.EnableSpellchecking,
            IsSavingEnergy = previousData.IsSavingEnergy,
            NextProviderNum = previousData.NextProviderNum,
            ShortcutSendBehavior = previousData.ShortcutSendBehavior,
            UpdateBehavior = previousData.UpdateBehavior,
            WorkspaceStorageBehavior = previousData.WorkspaceStorageBehavior,
            WorkspaceStorageTemporaryMaintenancePolicy = previousData.WorkspaceStorageTemporaryMaintenancePolicy,
        };
    }

    private static DataV4 MigrateV3ToV4(ILogger<SettingsManager> logger, DataV1V3 previousConfig)
    {
        //
        // Summary:
        // We grouped the settings into different categories.
        //
        
        logger.LogInformation("Migrating from v3 to v4...");
        return new()
        {
            Version = Version.V4,
            Providers = previousConfig.Providers,
            NextProviderNum = previousConfig.NextProviderNum,
            
            App = new(x => x.App)
            {
                EnableSpellchecking = previousConfig.EnableSpellchecking,
                IsSavingEnergy = previousConfig.IsSavingEnergy,
                UpdateBehavior = previousConfig.UpdateBehavior,
                NavigationBehavior = previousConfig.NavigationBehavior,
            },
            
            Chat = new()
            {
                ShortcutSendBehavior = previousConfig.ShortcutSendBehavior,
                PreselectOptions = previousConfig.PreselectChatOptions,
                PreselectedProvider = previousConfig.PreselectedChatProvider,
            },
            
            Workspace = new()
            {
                StorageBehavior = previousConfig.WorkspaceStorageBehavior,
                StorageTemporaryMaintenancePolicy = previousConfig.WorkspaceStorageTemporaryMaintenancePolicy,
            },
            
            IconFinder = new()
            {
                PreselectOptions = previousConfig.PreselectIconOptions,
                PreselectedProvider = previousConfig.PreselectedIconProvider,
                PreselectedSource = previousConfig.PreselectedIconSource,
            },
            
            Translation = new()
            {
                PreselectLiveTranslation = previousConfig.PreselectLiveTranslation,
                DebounceIntervalMilliseconds = previousConfig.LiveTranslationDebounceIntervalMilliseconds,
                PreselectOptions = previousConfig.PreselectTranslationOptions,
                PreselectedProvider = previousConfig.PreselectedTranslationProvider,
                PreselectedTargetLanguage = previousConfig.PreselectedTranslationTargetLanguage,
                PreselectOtherLanguage = previousConfig.PreselectTranslationOtherLanguage,
                HideWebContentReader = previousConfig.HideWebContentReaderForTranslation,
                PreselectContentCleanerAgent = previousConfig.PreselectContentCleanerAgentForTranslation,
                PreselectWebContentReader = previousConfig.PreselectWebContentReaderForTranslation,
            },
            
            Coding = new()
            {
                PreselectOptions = previousConfig.PreselectCodingOptions,
                PreselectedProvider = previousConfig.PreselectedCodingProvider,
                PreselectedProgrammingLanguage = previousConfig.PreselectedCodingLanguage,
                PreselectedOtherProgrammingLanguage = previousConfig.PreselectedCodingOtherLanguage,
                PreselectCompilerMessages = previousConfig.PreselectCodingCompilerMessages,
            },
            
            TextSummarizer = new()
            {
                PreselectOptions = previousConfig.PreselectTextSummarizerOptions,
                PreselectedComplexity = previousConfig.PreselectedTextSummarizerComplexity,
                PreselectedProvider = previousConfig.PreselectedTextSummarizerProvider,
                PreselectedTargetLanguage = previousConfig.PreselectedTextSummarizerTargetLanguage,
                PreselectedOtherLanguage = previousConfig.PreselectedTextSummarizerOtherLanguage,
                PreselectedExpertInField = previousConfig.PreselectedTextSummarizerExpertInField,
                HideWebContentReader = previousConfig.HideWebContentReaderForTextSummarizer,
                PreselectContentCleanerAgent = previousConfig.PreselectContentCleanerAgentForTextSummarizer,
                PreselectWebContentReader = previousConfig.PreselectWebContentReaderForTextSummarizer,
            },
            
            TextContentCleaner = new()
            {
                PreselectAgentOptions = previousConfig.PreselectAgentTextContentCleanerOptions,
                PreselectedAgentProvider = previousConfig.PreselectedAgentTextContentCleanerProvider,
            },
        };
    }
    
    private static Data MigrateV4ToV5(ILogger<SettingsManager> logger, DataV4 previousConfig)
    {
        //
        // Summary:
        // We renamed the LLM provider enum.
        //
        
        logger.LogInformation("Migrating from v4 to v5...");
        return new()
        {
            Version = Version.V5,
            Providers = previousConfig.Providers.MigrateFromV4ToV5(),
            LLMProviders = previousConfig.LLMProviders,
            Profiles = previousConfig.Profiles,
            NextProviderNum = previousConfig.NextProviderNum,
            NextProfileNum = previousConfig.NextProfileNum,
            App = previousConfig.App,
            Chat = previousConfig.Chat,
            Workspace = previousConfig.Workspace,
            IconFinder = previousConfig.IconFinder,
            Translation = previousConfig.Translation,
            Coding = previousConfig.Coding,
            TextSummarizer = previousConfig.TextSummarizer,
            TextContentCleaner = previousConfig.TextContentCleaner,
            Agenda = previousConfig.Agenda,
            GrammarSpelling = previousConfig.GrammarSpelling,
            RewriteImprove = previousConfig.RewriteImprove,
            EMail = previousConfig.EMail,
            LegalCheck = previousConfig.LegalCheck,
            Synonyms = previousConfig.Synonyms,
            MyTasks = previousConfig.MyTasks,
        };
    }
}