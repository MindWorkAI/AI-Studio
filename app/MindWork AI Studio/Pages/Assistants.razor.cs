using AIStudio.Components;
using AIStudio.AIOperations.PluginAudit;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

public partial class Assistants : MSGComponentBase
{
    private static readonly AssistantCategoryKey COMMON = new(PluginCategory.COMMON);
    private static readonly AssistantCategoryKey BUSINESS = new(PluginCategory.BUSINESS);
    private static readonly AssistantCategoryKey EDUCATION = new(PluginCategory.EDUCATION);
    private static readonly AssistantCategoryKey SOFTWARE_DEVELOPMENT = new(PluginCategory.SOFTWARE_DEVELOPMENT);

    private readonly AssistantCategoryRegistry categoryRegistry = new();

    private bool isAutoAuditing;

    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED, Event.PLUGINS_RELOADED ]);
        this.categoryRegistry.ShowsAnyCategoryChanged += this.StateHasChanged;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.categoryRegistry.ShowsAnyCategoryChanged -= this.StateHasChanged;
        base.DisposeResources();
    }

    /// <summary>
    /// Gets whether the user hid every category the page would show, so that it shows none.
    /// </summary>
    /// <remarks>
    /// Without a hint, the page would look broken. When your organization hid or disabled every
    /// assistant instead, the gear brings nothing back, so we keep quiet then.
    /// </remarks>
    private bool HidesEveryCategory
    {
        get
        {
            var page = this.SettingsManager.ConfigurationData.AssistantsPage;
            return this.categoryRegistry.ShowsAnyCategory is false && (page.HiddenCategories.Count > 0 || page.HiddenCustomCategories.Count > 0);
        }
    }

    private async Task OpenSettingsDialogAsync()
    {
        var categories = AssistantPageSections.SelectForSettings(this.categoryRegistry.ShownCategories, this.SettingsManager.ConfigurationData.App, this.SettingsManager.ConfigurationData.AssistantsPage);
        var dialogParameters = new DialogParameters<SettingsDialogAssistantsPage>
        {
            { x => x.Categories, categories },
        };

        await this.DialogService.ShowAsync<SettingsDialogAssistantsPage>(T("Assistant Settings"), dialogParameters, DialogOptions.FULLSCREEN);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await this.TryAutoAuditAssistantsAsync();
    }

    private IReadOnlyCollection<PluginAssistants> AssistantPlugins =>
        PluginFactory.RunningPlugins.OfType<PluginAssistants>()
            .Where(plugin => this.SettingsManager.IsAssistantPluginAvailable(plugin))
            .ToList();

    /// <summary>
    /// Groups the enabled assistant plugins into the sections of the page.
    /// </summary>
    /// <remarks>
    /// The page asks for the sections once per rendering, so that the plugins and their order stay the
    /// same within one rendering.
    /// </remarks>
    private IReadOnlyList<AssistantPageSection<PluginAssistants>> CreateSections() => AssistantPageSections.Build(this.AssistantPlugins, plugin => plugin.AssistantTitle);

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
                if (audit.Level is PluginAuditLevel.UNKNOWN)
                {
                    await MessageBus.INSTANCE.SendError(new (AppIcons.AGENT, string.Format(this.TA("The automatic security audit for the assistant plugin '{0}' failed. Please run it manually from the plugins page."), assistantPlugin.Name)));
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

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.PLUGINS_RELOADED)
            await this.TryAutoAuditAssistantsAsync();

        if (triggeredEvent is Event.CONFIGURATION_CHANGED or Event.PLUGINS_RELOADED)
            await this.InvokeAsync(this.StateHasChanged);
    }
}
