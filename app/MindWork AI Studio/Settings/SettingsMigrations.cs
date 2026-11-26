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
                var configV16 = MigrateV5ToV6(logger, configV15);
                return MigrateV6ToV7(logger, configV16);

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
                var configV26 = MigrateV5ToV6(logger, configV25);
                return MigrateV6ToV7(logger, configV26);

            case Version.V3:
                var configV3 = JsonSerializer.Deserialize<DataV1V3>(configData, jsonOptions);
                if (configV3 is null)
                {
                    logger.LogError("Failed to parse the v3 configuration. Using default values.");
                    return new();
                }

                var configV34 = MigrateV3ToV4(logger, configV3);
                var configV35 = MigrateV4ToV5(logger, configV34);
                var configV36 = MigrateV5ToV6(logger, configV35);
                return MigrateV6ToV7(logger, configV36);

            case Version.V4:
                var configV4 = JsonSerializer.Deserialize<DataV4>(configData, jsonOptions);
                if (configV4 is null)
                {
                    logger.LogError("Failed to parse the v4 configuration. Using default values.");
                    return new();
                }

                var configV45 = MigrateV4ToV5(logger, configV4);
                var configV46 = MigrateV5ToV6(logger, configV45);
                return MigrateV6ToV7(logger, configV46);

            case Version.V5:
                var configV5 = JsonSerializer.Deserialize<DataV5>(configData, jsonOptions);
                if (configV5 is null)
                {
                    logger.LogError("Failed to parse the v5 configuration. Using default values.");
                    return new();
                }

                var configV56 = MigrateV5ToV6(logger, configV5);
                return MigrateV6ToV7(logger, configV56);

            case Version.V6:
                var configV6 = JsonSerializer.Deserialize<DataV6>(configData, jsonOptions);
                if (configV6 is null)
                {
                    logger.LogError("Failed to parse the v6 configuration. Using default values.");
                    return new();
                }

                return MigrateV6ToV7(logger, configV6);

            default:
                logger.LogInformation("No configuration migration is needed.");
                var configV7 = JsonSerializer.Deserialize<Data>(configData, jsonOptions);
                if (configV7 is null)
                {
                    logger.LogError("Failed to parse the v7 configuration. Using default values.");
                    return new();
                }

                return configV7;
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

    private static DataV6 MigrateV5ToV6(ILogger<SettingsManager> logger, DataV5 previousConfig)
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
            Confidence = new()
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

    /// <summary>
    /// The names of the managed settings which changed with version V7, by their former names.
    /// </summary>
    /// <remarks>
    /// The keys of ManagedLockedConfigurations, ManagedEditableDefaults, and ManagedUserValueSnapshots
    /// name a setting by its class and its property, so the former names of the AI tasks live on there.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> MANAGED_SETTING_NAMES_V6_TO_V7 = new Dictionary<string, string>
    {
        ["DataTextContentCleaner.PreselectAgentOptions"] = "DataTextContentCleaner.PreselectOptions",
        ["DataTextContentCleaner.PreselectedAgentProvider"] = "DataTextContentCleaner.PreselectedProvider",
        ["DataAgentDataSourceSelection.PreselectAgentOptions"] = "DataDataSourceSelection.PreselectOptions",
        ["DataAgentDataSourceSelection.PreselectedAgentProvider"] = "DataDataSourceSelection.PreselectedProvider",
        ["DataAgentDataSourceSelection.MinimumProviderConfidence"] = "DataDataSourceSelection.MinimumProviderConfidence",
        ["DataAgentRetrievalContextValidation.EnableRetrievalContextValidation"] = "DataRetrievalContextValidation.EnableRetrievalContextValidation",
        ["DataAgentRetrievalContextValidation.PreselectAgentOptions"] = "DataRetrievalContextValidation.PreselectOptions",
        ["DataAgentRetrievalContextValidation.PreselectedAgentProvider"] = "DataRetrievalContextValidation.PreselectedProvider",
        ["DataAgentRetrievalContextValidation.MinimumProviderConfidence"] = "DataRetrievalContextValidation.MinimumProviderConfidence",
        ["DataAgentRetrievalContextValidation.NumParallelValidations"] = "DataRetrievalContextValidation.NumParallelValidations",
        ["DataAssistantPluginAudit.PreselectedAgentProvider"] = "DataAssistantPluginAudit.PreselectedProvider",
    };

    private static Data MigrateV6ToV7(ILogger<SettingsManager> logger, DataV6 previousConfig)
    {
        //
        // Summary:
        // The background agents became AI tasks, so their settings no longer speak of agents: the data
        // source selection and the retrieval context validation got new classes, and every AI task
        // calls its options and its provider like the assistants do. The managed settings follow.
        //

        logger.LogInformation("Migrating from v6 to v7...");
        return new()
        {
            Version = Version.V7,
            Providers = previousConfig.Providers,
            Confidence = previousConfig.Confidence,
            DataSourceSecurity = previousConfig.DataSourceSecurity,
            EmbeddingProviders = previousConfig.EmbeddingProviders,
            TranscriptionProviders = previousConfig.TranscriptionProviders,
            DataSources = previousConfig.DataSources,
            Mailboxes = previousConfig.Mailboxes,
            MailboxSettings = previousConfig.MailboxSettings,
            Profiles = previousConfig.Profiles,
            ChatTemplates = previousConfig.ChatTemplates,
            EnabledPlugins = previousConfig.EnabledPlugins,
            ManagedEditableDefaults = RenameManagedSettingsV6ToV7(previousConfig.ManagedEditableDefaults),
            ManagedLockedConfigurations = RenameManagedSettingsV6ToV7(previousConfig.ManagedLockedConfigurations),
            ManagedUserValueSnapshots = RenameManagedSettingsV6ToV7(previousConfig.ManagedUserValueSnapshots),
            AssistantPluginAudits = previousConfig.AssistantPluginAudits,
            AppliedEnterpriseAssistantActivations = previousConfig.AppliedEnterpriseAssistantActivations,
            StoredDataPathsAreRelative = previousConfig.StoredDataPathsAreRelative,
            NextProviderNum = previousConfig.NextProviderNum,
            NextEmbeddingNum = previousConfig.NextEmbeddingNum,
            NextTranscriptionNum = previousConfig.NextTranscriptionNum,
            NextDataSourceNum = previousConfig.NextDataSourceNum,
            NextProfileNum = previousConfig.NextProfileNum,
            NextChatTemplateNum = previousConfig.NextChatTemplateNum,
            NextDocumentAnalysisPolicyNum = previousConfig.NextDocumentAnalysisPolicyNum,
            App = previousConfig.App,
            Chat = previousConfig.Chat,
            AssistantsPage = previousConfig.AssistantsPage,
            Workspace = previousConfig.Workspace,
            IconFinder = previousConfig.IconFinder,
            Translation = previousConfig.Translation,
            Coding = previousConfig.Coding,
            ERI = previousConfig.ERI,
            DocumentAnalysis = previousConfig.DocumentAnalysis,
            BatchProcessing = previousConfig.BatchProcessing,
            MandatoryInformation = previousConfig.MandatoryInformation,
            TextSummarizer = previousConfig.TextSummarizer,
            SWOTAnalysis = previousConfig.SWOTAnalysis,
            TextContentCleaner = new(x => x.TextContentCleaner)
            {
                PreselectOptions = previousConfig.TextContentCleaner.PreselectAgentOptions,
                PreselectedProvider = previousConfig.TextContentCleaner.PreselectedAgentProvider,
                MinimumProviderConfidence = previousConfig.TextContentCleaner.MinimumProviderConfidence,
            },
            DataSourceSelection = new(x => x.DataSourceSelection)
            {
                PreselectOptions = previousConfig.AgentDataSourceSelection.PreselectAgentOptions,
                PreselectedProvider = previousConfig.AgentDataSourceSelection.PreselectedAgentProvider,
                MinimumProviderConfidence = previousConfig.AgentDataSourceSelection.MinimumProviderConfidence,
            },
            RetrievalContextValidation = new(x => x.RetrievalContextValidation)
            {
                EnableRetrievalContextValidation = previousConfig.AgentRetrievalContextValidation.EnableRetrievalContextValidation,
                PreselectOptions = previousConfig.AgentRetrievalContextValidation.PreselectAgentOptions,
                PreselectedProvider = previousConfig.AgentRetrievalContextValidation.PreselectedAgentProvider,
                MinimumProviderConfidence = previousConfig.AgentRetrievalContextValidation.MinimumProviderConfidence,
                NumParallelValidations = previousConfig.AgentRetrievalContextValidation.NumParallelValidations,
            },
            AssistantPluginAudit = new(x => x.AssistantPluginAudit)
            {
                RequireAuditBeforeActivation = previousConfig.AssistantPluginAudit.RequireAuditBeforeActivation,
                PreselectedProvider = previousConfig.AssistantPluginAudit.PreselectedAgentProvider,
                MinimumProviderConfidence = previousConfig.AssistantPluginAudit.MinimumProviderConfidence,
                MinimumLevel = previousConfig.AssistantPluginAudit.MinimumLevel,
                BlockActivationBelowMinimum = previousConfig.AssistantPluginAudit.BlockActivationBelowMinimum,
                AutomaticallyAuditAssistants = previousConfig.AssistantPluginAudit.AutomaticallyAuditAssistants,
                EnterpriseApprovedPlugins = previousConfig.AssistantPluginAudit.EnterpriseApprovedPlugins,
            },
            Agenda = previousConfig.Agenda,
            GrammarSpelling = previousConfig.GrammarSpelling,
            RewriteImprove = previousConfig.RewriteImprove,
            PromptOptimizer = previousConfig.PromptOptimizer,
            EMail = previousConfig.EMail,
            SlideBuilder = previousConfig.SlideBuilder,
            AssistantBuilder = previousConfig.AssistantBuilder,
            VisualBriefing = previousConfig.VisualBriefing,
            BeyondFactsLab = previousConfig.BeyondFactsLab,
            ModelComparison = previousConfig.ModelComparison,
            LegalCheck = previousConfig.LegalCheck,
            Synonyms = previousConfig.Synonyms,
            MyTasks = previousConfig.MyTasks,
            JobPostings = previousConfig.JobPostings,
            BiasOfTheDay = previousConfig.BiasOfTheDay,
            I18N = previousConfig.I18N,
            Tools = previousConfig.Tools,
        };
    }

    private static Dictionary<string, TValue> RenameManagedSettingsV6ToV7<TValue>(Dictionary<string, TValue> managedStates)
    {
        var renamedStates = new Dictionary<string, TValue>(managedStates.Count);
        foreach (var (settingName, state) in managedStates)
            renamedStates[MANAGED_SETTING_NAMES_V6_TO_V7.GetValueOrDefault(settingName, settingName)] = state;

        return renamedStates;
    }
}