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
                return MigrateV3ToV4(logger, configV1);

            case Version.V2:
                var configV2 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV2 is null)
                {
                    logger.LogError("Failed to parse the v2 configuration. Using default values.");
                    return new();
                }

                configV2 = MigrateV2ToV3(logger, configV2);
                return MigrateV3ToV4(logger, configV2);

            case Version.V3:
                var configV3 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV3 is null)
                {
                    logger.LogError("Failed to parse the v3 configuration. Using default values.");
                    return new();
                }

                return MigrateV3ToV4(logger, configV3);

            default:
                logger.LogInformation("No configuration migration is needed.");
                var configV4 = JsonSerializer.Deserialize<Data>(configData, jsonOptions);
                if (configV4 is null)
                {
                    logger.LogError("Failed to parse the v4 configuration. Using default values.");
                    return new();
                }

                return configV4;
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
            
            Providers = previousData.Providers.Select(provider => provider with { IsSelfHosted = false, Hostname = "" }).ToList(),
            
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

    private static Data MigrateV3ToV4(ILogger<SettingsManager> logger, DataV1V3 previousConfig)
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
            
            App = new()
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
}