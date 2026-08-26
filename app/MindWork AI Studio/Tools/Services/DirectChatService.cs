using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Tools.Services;

public sealed record DirectChatStartResult(ChatStartRequest? Request, string ErrorMessage);

public sealed class DirectChatService(SettingsManager settingsManager, DataSourceService dataSourceService, ILogger<DirectChatService> logger)
{
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(DirectChatService).Namespace, nameof(DirectChatService));

    public async Task<DirectChatStartResult> TryCreateAssistantChatAsync(PluginAssistants assistantPlugin)
    {
        if (assistantPlugin.ChatLaunchConfiguration is not { } launchConfiguration)
            return new(null, TB("The assistant plugin does not contain a valid chat launch configuration."));

        var providerResult = this.ResolveProvider(launchConfiguration.ProviderId);
        if (providerResult.Provider == ProviderSettings.NONE)
            return new(null, providerResult.ErrorMessage);

        var profileResult = this.ResolveProfile(launchConfiguration.ProfileId);
        var profile = profileResult.Profile;
        if (profile is null)
            return new(null, profileResult.ErrorMessage);

        var chatTemplateResult = this.ResolveChatTemplate(launchConfiguration.ChatTemplateId);
        var chatTemplate = chatTemplateResult.ChatTemplate;
        if (chatTemplate is null)
            return new(null, chatTemplateResult.ErrorMessage);

        var dataSourceOptionsResult = await this.ResolveDataSourceOptionsAsync(providerResult.Provider, launchConfiguration.DataSourceIds);
        var dataSourceOptions = dataSourceOptionsResult.Options;
        if (dataSourceOptions is null)
            return new(null, dataSourceOptionsResult.ErrorMessage);

        Guid workspaceId;
        try
        {
            workspaceId = await WorkspaceBehaviour.ResolveOrCreateWorkspaceIdByNameAsync(launchConfiguration.WorkspaceName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Assistant plugin '{PluginName}' could not resolve or create workspace '{WorkspaceName}'.", assistantPlugin.Name, launchConfiguration.WorkspaceName);
            return new(null, string.Format(TB("The workspace '{0}' could not be opened or created."), launchConfiguration.WorkspaceName));
        }

        if (workspaceId == Guid.Empty)
        {
            logger.LogWarning("Assistant plugin '{PluginName}' could not resolve or create workspace '{WorkspaceName}'.", assistantPlugin.Name, launchConfiguration.WorkspaceName);
            return new(null, string.Format(TB("The workspace '{0}' could not be opened or created."), launchConfiguration.WorkspaceName));
        }

        var chatThread = new ChatThread
        {
            IncludeDateTime = true,
            SelectedProvider = providerResult.Provider.Id,
            SelectedProfile = profile.Id,
            SelectedChatTemplate = chatTemplate.Id,
            SystemPrompt = SystemPrompts.DEFAULT,
            WorkspaceId = workspaceId,
            ChatId = Guid.NewGuid(),
            Name = assistantPlugin.AssistantTitle,
            DataSourceOptions = dataSourceOptions,
            Blocks = chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : chatTemplate.ExampleConversation.Select(block => block.DeepClone()).ToList(),
        };

        return new(new(chatThread, ApplySelectedChatTemplateToComposer: true, PreserveDataSourceOptions: launchConfiguration.DataSourceIds is not null), string.Empty);
    }

    private (ProviderSettings Provider, string ErrorMessage) ResolveProvider(Guid? providerId)
    {
        if (providerId is null)
        {
            var defaultProvider = settingsManager.GetPreselectedProvider(Components.CHAT);
            return defaultProvider == ProviderSettings.NONE
                ? new(ProviderSettings.NONE, TB("No provider is currently available for this assistant chat launcher."))
                : new(defaultProvider, string.Empty);
        }

        var provider = settingsManager.GetAllProviders().FirstOrDefault(candidate =>
            Guid.TryParse(candidate.Id, out var candidateId) && candidateId == providerId.Value);
        
        if (provider is null)
            return new(ProviderSettings.NONE, string.Format(TB("The assistant chat launcher references provider '{0}', but that provider does not exist."), providerId));

        if (!settingsManager.IsProviderConfident(provider, Components.CHAT))
            return new(ProviderSettings.NONE, string.Format(TB("The provider '{0}' selected by the assistant chat launcher is not permitted for chats at the required confidence level."), provider.InstanceName));

        return new(provider, string.Empty);
    }

    private (Profile? Profile, string ErrorMessage) ResolveProfile(Guid? profileId)
    {
        if (profileId is null)
            return new(settingsManager.GetPreselectedProfile(Components.CHAT), string.Empty);

        if (profileId == Guid.Empty)
            return new(Profile.NO_PROFILE, string.Empty);

        var profile = settingsManager.ConfigurationData.Profiles.FirstOrDefault(candidate =>
            Guid.TryParse(candidate.Id, out var candidateId) && candidateId == profileId.Value);
        
        return profile is null
            ? new(null, string.Format(TB("The assistant chat launcher references profile '{0}', but that profile does not exist."), profileId))
            : new(profile, string.Empty);
    }

    private (ChatTemplate? ChatTemplate, string ErrorMessage) ResolveChatTemplate(Guid? chatTemplateId)
    {
        if (chatTemplateId is null)
            return new(settingsManager.GetPreselectedChatTemplate(Components.CHAT), string.Empty);

        if (chatTemplateId == Guid.Empty)
            return new(ChatTemplate.NO_CHAT_TEMPLATE, string.Empty);

        var chatTemplate = settingsManager.ConfigurationData.ChatTemplates.FirstOrDefault(candidate =>
            Guid.TryParse(candidate.Id, out var candidateId) && candidateId == chatTemplateId.Value);
        
        return chatTemplate is null
            ? new(null, string.Format(TB("The assistant chat launcher references chat template '{0}', but that template does not exist."), chatTemplateId))
            : new(chatTemplate, string.Empty);
    }

    private async Task<(DataSourceOptions? Options, string ErrorMessage)> ResolveDataSourceOptionsAsync(ProviderSettings provider, IReadOnlyList<Guid>? dataSourceIds)
    {
        if (dataSourceIds is null)
            return new(settingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy(), string.Empty);

        var requestedDataSources = new List<IDataSource>(dataSourceIds.Count);
        foreach (var dataSourceId in dataSourceIds)
        {
            var dataSource = settingsManager.ConfigurationData.DataSources.FirstOrDefault(candidate =>
                Guid.TryParse(candidate.Id, out var candidateId) && candidateId == dataSourceId);
            
            if (dataSource is null)
                return new(null, string.Format(TB("The assistant chat launcher references data source '{0}', but that data source does not exist."), dataSourceId));

            requestedDataSources.Add(dataSource);
        }

        IReadOnlyList<IDataSource> availableDataSources;
        try
        {
            availableDataSources = await dataSourceService.GetAllowedDataSources(provider, requestedDataSources);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The data sources configured by an assistant chat launcher could not be checked.");
            return new(null, TB("The data sources selected by the assistant chat launcher could not be checked. No chat was created."));
        }

        var availableSelectedIds = availableDataSources.Select(source => source.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unavailableDataSources = requestedDataSources.Where(source => !availableSelectedIds.Contains(source.Id)).Select(source => source.Name).ToList();
        if (unavailableDataSources.Count > 0)
            return new(null, string.Format(TB("The following data sources selected by the assistant chat launcher are currently unavailable or not permitted for the selected provider: {0}"), string.Join(", ", unavailableDataSources)));

        var standardOptions = settingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions;
        return new(new()
        {
            DisableDataSources = false,
            AutomaticDataSourceSelection = false,
            AutomaticValidation = standardOptions.AutomaticValidation,
            PreselectedDataSourceIds = requestedDataSources.Select(source => source.Id).ToList(),
        }, string.Empty);
    }
}