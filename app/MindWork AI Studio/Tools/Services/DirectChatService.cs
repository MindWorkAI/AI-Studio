using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.ToolCallingSystem;
using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Tools.Services;

public sealed class DirectChatService(SettingsManager settingsManager, DataSourceService dataSourceService, ToolRegistry toolRegistry, ILogger<DirectChatService> logger)
{
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(DirectChatService).Namespace, nameof(DirectChatService));

    public async Task<DirectChatStartResult> TryCreateAssistantChatAsync(PluginAssistants assistantPlugin)
    {
        if (assistantPlugin.ChatLaunchConfiguration is not { } launchConfiguration)
            return new(null, TB("The assistant plugin does not contain a valid chat launch configuration."));

        var providerResult = this.ResolveProvider(launchConfiguration.ProviderId);
        if (providerResult.IsExplicit && providerResult.Provider == ProviderSettings.NONE)
            return new(null, providerResult.ErrorMessage);

        var profileResult = this.ResolveProfile(launchConfiguration.ProfileId);
        var profile = profileResult.Profile;
        if (profile is null)
            return new(null, profileResult.ErrorMessage);

        var chatTemplateResult = this.ResolveChatTemplate(launchConfiguration.ChatTemplateId);
        var chatTemplate = chatTemplateResult.ChatTemplate;
        if (chatTemplate is null)
            return new(null, chatTemplateResult.ErrorMessage);

        //
        // A chat template that forbids profiles wins over a configured profile: the chat disables
        // its profile selection for such templates, so keeping the profile would pin one that the
        // user can neither see nor change. We drop it instead of failing the whole launch.
        //
        if (!chatTemplate.AllowProfileUsage && profile != Profile.NO_PROFILE)
        {
            logger.LogWarning(
                "Assistant plugin '{PluginName}' selects the profile '{ProfileName}', but its chat template '{ChatTemplateName}' does not allow profiles. The chat starts without a profile.",
                assistantPlugin.Name, profile.GetSafeName(), chatTemplate.GetSafeName());

            profile = Profile.NO_PROFILE;
        }

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

        //
        // Only the tools the user could have switched on themselves. A launcher may name one whose
        // settings are incomplete — an unconfigured web search, say — and starting the chat with it
        // enabled would show a state the user cannot produce by hand and cannot fix from the chat.
        // Null keeps the chat's own defaults, which is what a launcher without tools wants.
        //
        var selectedToolIds = launchConfiguration.ToolIds is null
            ? null
            : await toolRegistry.FilterSelectableToolIdsAsync(Components.CHAT, launchConfiguration.ToolIds);

        var chatThread = new ChatThread
        {
            IncludeDateTime = true,
            SelectedProvider = providerResult.Provider == ProviderSettings.NONE ? string.Empty : providerResult.Provider.Id,
            SelectedProfile = profile.Id,
            SelectedChatTemplate = chatTemplate.Id,
            // The provider confidence is checked later, when the chat sends a message:
            SelectedToolIds = selectedToolIds,
            SystemPrompt = SystemPrompts.DEFAULT,
            WorkspaceId = workspaceId,
            ChatId = Guid.NewGuid(),
            Name = assistantPlugin.AssistantTitle,
            DataSourceOptions = dataSourceOptions,
            Blocks = chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE ? [] : chatTemplate.ExampleConversation.Select(block => block.DeepClone()).ToList(),
        };

        return new(new(chatThread, ApplySelectedChatTemplateToComposer: true, PreserveDataSourceOptions: launchConfiguration.DataSourceIds is not null), string.Empty);
    }

    private (ProviderSettings Provider, bool IsExplicit, string ErrorMessage) ResolveProvider(Guid? providerId)
    {
        //
        // The launcher does not name a provider, so it wants the chat defaults. We resolve them
        // exactly like the chat does when it loads a chat without a provider. When no default can
        // be determined, we do not fail: the chat opens with an empty provider selection and the
        // user picks a provider there, just like for any other new chat.
        //
        if (providerId is null)
            return new(settingsManager.GetChatProviderForLoadedChat(), false, string.Empty);

        //
        // GetProviderById does not apply any confidence filtering, so we check the provider
        // ourselves afterwards, exactly as its documentation demands:
        //
        var provider = settingsManager.GetProviderById(providerId.Value.ToString());
        if (provider == ProviderSettings.NONE)
            return new(ProviderSettings.NONE, true, string.Format(TB("The assistant chat launcher references provider '{0}', but that provider does not exist."), providerId));

        if (!settingsManager.IsProviderConfident(provider, Components.CHAT))
            return new(ProviderSettings.NONE, true, string.Format(TB("The provider '{0}' selected by the assistant chat launcher is not permitted for chats at the required confidence level."), provider.InstanceName));

        return new(provider, true, string.Empty);
    }

    private (Profile? Profile, string ErrorMessage) ResolveProfile(Guid? profileId)
    {
        if (profileId is null)
            return new(settingsManager.GetPreselectedProfile(Components.CHAT), string.Empty);

        // The launcher explicitly wants no profile:
        if (profileId == Guid.Empty)
            return new(Profile.NO_PROFILE, string.Empty);

        //
        // We already handled the empty GUID above, so GetProfileById returning the no-profile
        // entry here can only mean that the referenced profile is gone:
        //
        var profile = settingsManager.GetProfileById(profileId.Value.ToString());
        return profile == Profile.NO_PROFILE
            ? new(null, string.Format(TB("The assistant chat launcher references profile '{0}', but that profile does not exist."), profileId))
            : new(profile, string.Empty);
    }

    private (ChatTemplate? ChatTemplate, string ErrorMessage) ResolveChatTemplate(Guid? chatTemplateId)
    {
        if (chatTemplateId is null)
            return new(settingsManager.GetPreselectedChatTemplate(Components.CHAT), string.Empty);

        // The launcher explicitly wants no chat template:
        if (chatTemplateId == Guid.Empty)
            return new(ChatTemplate.NO_CHAT_TEMPLATE, string.Empty);

        //
        // We already handled the empty GUID above, so GetChatTemplateById returning the
        // no-template entry here can only mean that the referenced template is gone:
        //
        var chatTemplate = settingsManager.GetChatTemplateById(chatTemplateId.Value.ToString());
        return chatTemplate == ChatTemplate.NO_CHAT_TEMPLATE
            ? new(null, string.Format(TB("The assistant chat launcher references chat template '{0}', but that template does not exist."), chatTemplateId))
            : new(chatTemplate, string.Empty);
    }

    private async Task<(DataSourceOptions? Options, string ErrorMessage)> ResolveDataSourceOptionsAsync(ProviderSettings provider, IReadOnlyList<Guid>? dataSourceIds)
    {
        if (dataSourceIds is null)
            return new(settingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy(), string.Empty);

        //
        // Deciding which data sources are permitted needs an effective provider. Without one,
        // the check below would report every requested source as unavailable, which would hide
        // the actual cause from the user:
        //
        if (provider == ProviderSettings.NONE)
            return new(null, TB("The assistant chat launcher selects data sources, but no provider is available for chats. Please choose a default provider for chats first. No chat was created."));

        var requestedDataSources = new List<IDataSource>(dataSourceIds.Count);
        foreach (var dataSourceId in dataSourceIds)
        {
            // Data sources have no lookup helper in the settings manager, so we match their ids
            // the same way the rest of the app does:
            var dataSourceIdText = dataSourceId.ToString();
            var dataSource = settingsManager.ConfigurationData.DataSources.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, dataSourceIdText, StringComparison.OrdinalIgnoreCase));

            if (dataSource is null)
                return new(null, string.Format(TB("The assistant chat launcher references data source '{0}', but that data source does not exist."), dataSourceId));

            requestedDataSources.Add(dataSource);
        }

        //
        // The options the launched chat will run under. We build them here already, because the
        // data-source check depends on them: they decide which agent providers take part, and an
        // agent with too little confidence makes a data source unavailable.
        //
        var standardOptions = settingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions;
        var launchedDataSourceOptions = new DataSourceOptions
        {
            DisableDataSources = false,
            AutomaticDataSourceSelection = false,
            AutomaticValidation = standardOptions.AutomaticValidation,
            PreselectedDataSourceIds = requestedDataSources.Select(source => source.Id).ToList(),
        };

        IReadOnlyList<IDataSource> availableDataSources;
        try
        {
            availableDataSources = await dataSourceService.GetAllowedDataSources(provider, launchedDataSourceOptions, requestedDataSources);
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

        return new(launchedDataSourceOptions, string.Empty);
    }
}