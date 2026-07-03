using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantSessions;
using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class AssistantBlock<TSettings> : MSGComponentBase where TSettings : IComponent
{
    [Parameter]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    public string Description { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.DisabledByDefault;

    [Parameter]
    public string ButtonText { get; set; } = "Start";

    [Parameter]
    public string Link { get; set; } = string.Empty;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public RenderFragment? SecurityBadge { get; set; }

    [Parameter]
    public Tools.Components Component { get; set; } = Tools.Components.NONE;

    /// <summary>
    /// Gets or sets the optional assistant session instance ID represented by this block.
    /// </summary>
    [Parameter]
    public string AssistantSessionInstanceId { get; set; } = string.Empty;

    [Parameter]
    public PreviewFeatures RequiredPreviewFeature { get; set; } = PreviewFeatures.NONE;

    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private AssistantSessionService AssistantSessionService { get; init; } = null!;
    
    private async Task OpenSettingsDialog()
    {
        if (!this.HasSettingsPanel)
            return;

        var dialogParameters = new DialogParameters();

        await this.DialogService.ShowAsync<TSettings>(T("Open Settings"), dialogParameters, DialogOptions.FULLSCREEN);
    }

    private string BorderColor => this.HasActiveSession ? this.ColorTheme.GetActivityIndicatorColor(this.SettingsManager) : this.SettingsManager.IsDarkMode switch
    {
        true => this.ColorTheme.GetCurrentPalette(this.SettingsManager).GrayDefault,
        false => this.ColorTheme.GetCurrentPalette(this.SettingsManager).GrayDefault,
    };

    private string BlockStyle => $"border-width: 3px; border-color: {this.BorderColor}; border-radius: 12px; border-style: solid; max-width: 20em;";

    private bool IsVisible => this.SettingsManager.IsAssistantVisible(this.Component, assistantName: this.Name, requiredPreviewFeature: this.RequiredPreviewFeature);

    private bool HasSettingsPanel => typeof(TSettings) != typeof(NoSettingsPanel);

    /// <summary>
    /// Gets whether this block represents an active assistant session.
    /// </summary>
    private bool HasActiveSession => string.IsNullOrWhiteSpace(this.AssistantSessionInstanceId)
        ? this.AssistantSessionService.GetSnapshots().Any(snapshot => snapshot.IsActive && snapshot.Key.Component == this.Component)
        : this.AssistantSessionService.GetSnapshots().Any(snapshot => snapshot.IsActive && snapshot.Key.InstanceId == this.AssistantSessionInstanceId);

    /// <summary>
    /// Refreshes the block when assistant session activity changes.
    /// </summary>
    /// <typeparam name="T">The message payload type.</typeparam>
    /// <param name="sendingComponent">The component that sent the message, if any.</param>
    /// <param name="triggeredEvent">The event that was triggered.</param>
    /// <param name="data">The message payload.</param>
    /// <returns>A task that completes after the message was processed.</returns>
    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.ASSISTANT_SESSION_CHANGED or Event.ASSISTANT_SESSION_FINISHED)
            this.StateHasChanged();

        return base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }
}