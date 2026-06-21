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
                var configV15 = MigrateV4ToV5(logger, configV14);
                return MigrateV5ToV6(logger, configV15);

            case Version.V2:
                var configV2 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV2 is null)
                {
                    logger.LogError("Failed to parse the v2 configuration. Using default values.");
                    return new();
                }

                configV2 = MigrateV2ToV3(logger, configV2);
                var configV24 = MigrateV3ToV4(logger, configV2);
                var configV25 = MigrateV4ToV5(logger, configV24);
                return MigrateV5ToV6(logger, configV25);

            case Version.V3:
                var configV3 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV3 is null)
                {
                    logger.LogError("Failed to parse the v3 configuration. Using default values.");
                    return new();
                }

                var configV34 = MigrateV3ToV4(logger, configV3);
                var configV35 = MigrateV4ToV5(logger, configV34);
                return MigrateV5ToV6(logger, configV35);

            case Version.V4:
                var configV4 = JsonSerializer.Deserialize<DataV4>(configData, jsonOptions);
                if (configV4 is null)
                {
                    logger.LogError("Failed to parse the v4 configuration. Using default values.");
                    return new();
                }

                var configV45 = MigrateV4ToV5(logger, configV4);
                return MigrateV5ToV6(logger, configV45);

            case Version.V5:
                var configV5 = JsonSerializer.Deserialize<DataV5>(configData, jsonOptions);
                if (configV5 is null)
                {
                    logger.LogError("Failed to parse the v5 configuration. Using default values.");
                    return new();
                }

                return MigrateV5ToV6(logger, configV5);

            default:
                logger.LogInformation("No configuration migration is needed.");
                var configV6 = JsonSerializer.Deserialize<Data>(configData, jsonOptions);
                if (configV6 is null)
                {
                    logger.LogError("Failed to parse the v6 configuration. Using default values.");
                    return new();
                }

                return configV6;
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
            UpdateInterval = previousData.UpdateInterval,
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
            UpdateInterval = previousData.UpdateInterval,
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
                UpdateInterval = previousConfig.UpdateInterval,
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

    private static DataV5 MigrateV4ToV5(ILogger<SettingsManager> logger, DataV4 previousConfig)
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

    private static Data MigrateV5ToV6(ILogger<SettingsManager> logger, DataV5 previousConfig)
    {
        //
        // Summary:
        // We moved confidence settings out of LLM provider settings.
        //

        logger.LogInformation("Migrating from v5 to v6...");
        return new()
        {
            Version = Version.V6,
            Providers = previousConfig.Providers,
            Confidence = new(x => x.Confidence)
            {
                EnforceGlobalMinimumConfidence = previousConfig.LLMProviders.EnforceGlobalMinimumConfidence,
                GlobalMinimumConfidence = previousConfig.LLMProviders.GlobalMinimumConfidence,
                ShowProviderConfidence = previousConfig.LLMProviders.ShowProviderConfidence,
                ConfidenceScheme = previousConfig.LLMProviders.ConfidenceScheme,
                CustomConfidenceScheme = previousConfig.LLMProviders.CustomConfidenceScheme,
            },
            EmbeddingProviders = previousConfig.EmbeddingProviders,
            TranscriptionProviders = previousConfig.TranscriptionProviders,
            DataSources = previousConfig.DataSources,
            Profiles = previousConfig.Profiles,
            ChatTemplates = previousConfig.ChatTemplates,
            EnabledPlugins = previousConfig.EnabledPlugins,
            ManagedEditableDefaults = previousConfig.ManagedEditableDefaults,
            AssistantPluginAudits = previousConfig.AssistantPluginAudits,
            NextProviderNum = previousConfig.NextProviderNum,
            NextEmbeddingNum = previousConfig.NextEmbeddingNum,
            NextTranscriptionNum = previousConfig.NextTranscriptionNum,
            NextDataSourceNum = previousConfig.NextDataSourceNum,
            NextProfileNum = previousConfig.NextProfileNum,
            NextChatTemplateNum = previousConfig.NextChatTemplateNum,
            NextDocumentAnalysisPolicyNum = previousConfig.NextDocumentAnalysisPolicyNum,
            App = previousConfig.App,
            Chat = previousConfig.Chat,
            Workspace = previousConfig.Workspace,
            IconFinder = previousConfig.IconFinder,
            Translation = previousConfig.Translation,
            Coding = previousConfig.Coding,
            ERI = previousConfig.ERI,
            DocumentAnalysis = previousConfig.DocumentAnalysis,
            MandatoryInformation = previousConfig.MandatoryInformation,
            TextSummarizer = previousConfig.TextSummarizer,
            TextContentCleaner = previousConfig.TextContentCleaner,
            AgentDataSourceSelection = previousConfig.AgentDataSourceSelection,
            AgentRetrievalContextValidation = previousConfig.AgentRetrievalContextValidation,
            AssistantPluginAudit = previousConfig.AssistantPluginAudit,
            Agenda = previousConfig.Agenda,
            GrammarSpelling = previousConfig.GrammarSpelling,
            RewriteImprove = previousConfig.RewriteImprove,
            PromptOptimizer = previousConfig.PromptOptimizer,
            EMail = previousConfig.EMail,
            SlideBuilder = previousConfig.SlideBuilder,
            LegalCheck = previousConfig.LegalCheck,
            Synonyms = previousConfig.Synonyms,
            MyTasks = previousConfig.MyTasks,
            JobPostings = previousConfig.JobPostings,
            BiasOfTheDay = previousConfig.BiasOfTheDay,
            I18N = previousConfig.I18N,
        };
    }
}