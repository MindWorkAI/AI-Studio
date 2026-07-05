using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Assistants : MSGComponentBase
{
    private bool isAutoAuditing;

    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    [Inject]
    private ILogger<Assistants> Logger { get; init; } = null!;
    
    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED ]);
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await this.TryAutoAuditAssistantsAsync();
    }

    private IReadOnlyCollection<PluginAssistants> AssistantPlugins =>
        PluginFactory.RunningPlugins.OfType<PluginAssistants>()
            .Where(plugin => this.SettingsManager.IsPluginEnabled(plugin))
            .ToList();

    private async Task TryAutoAuditAssistantsAsync()
    {
        if (this.isAutoAuditing || !this.SettingsManager.ConfigurationData.AssistantPluginAudit.AutomaticallyAuditAssistants)
            return;

        this.isAutoAuditing = true;

        try
        {
            var wasConfigurationChanged = false;
            var assistantPlugins = PluginFactory.RunningPlugins.OfType<PluginAssistants>().ToList();
            foreach (var assistantPlugin in assistantPlugins)
            {
                var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
                if (!securityState.RequiresAudit)
                    continue;

                var audit = await this.AssistantPluginAuditService.RunAuditAsync(assistantPlugin);
                if (audit.Level is AssistantAuditLevel.UNKNOWN)
                {
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.SettingsSuggest, string.Format(this.T("The automatic security audit for the assistant plugin '{0}' failed. Please run it manually from the plugins page."), assistantPlugin.Name)));
                    continue;
                }

                this.UpsertAuditCard(audit);
                wasConfigurationChanged = true;
            }

            if (!wasConfigurationChanged)
                return;

            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
        finally
        {
            this.isAutoAuditing = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void UpsertAuditCard(PluginAssistantAudit audit)
    {
        var audits = this.SettingsManager.ConfigurationData.AssistantPluginAudits;
        var existingIndex = audits.FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }

    private async Task StartAssistantPluginAsync(PluginAssistants assistantPlugin)
    {
        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
        if (!securityState.CanStartAssistant)
            return;

        if (!assistantPlugin.StartsChatDirectly)
        {
            this.NavigationManager.NavigateTo($"{Routes.ASSISTANT_DYNAMIC}?assistantId={assistantPlugin.Id}");
            return;
        }

        var chatThread = await this.TryCreateDirectChatThreadAsync(assistantPlugin);
        if (chatThread is null)
            return;

        MessageBus.INSTANCE.DeferMessage(this, Event.SEND_TO_CHAT, chatThread);
        this.NavigationManager.NavigateTo(Routes.CHAT);
    }

    private async Task<ChatThread?> TryCreateDirectChatThreadAsync(PluginAssistants assistantPlugin)
    {
        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateWorkspaceIdByNameAsync(assistantPlugin.LaunchWorkspaceName);
        if (workspaceId == Guid.Empty)
        {
            this.Logger.LogWarning("Assistant plugin '{PluginName}' could not resolve or create workspace '{WorkspaceName}'.", assistantPlugin.Name, assistantPlugin.LaunchWorkspaceName);
            return null;
        }

        return new ChatThread
        {
            IncludeDateTime = true,
            SelectedProvider = string.Empty,
            SelectedProfile = string.Empty,
            SelectedChatTemplate = string.Empty,
            SystemPrompt = SystemPrompts.DEFAULT,
            WorkspaceId = workspaceId,
            ChatId = Guid.NewGuid(),
            Name = assistantPlugin.AssistantTitle,
            DataSourceOptions = this.SettingsManager.ConfigurationData.Chat.PreselectedDataSourceOptions.CreateCopy(),
            Blocks = [],
        };
    }

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.PLUGINS_RELOADED)
            await this.TryAutoAuditAssistantsAsync();

        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            await this.InvokeAsync(this.StateHasChanged);
    }
}
