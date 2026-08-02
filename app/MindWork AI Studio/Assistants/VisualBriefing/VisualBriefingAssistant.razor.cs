using AIStudio.Assistants.SlideBuilder;
using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;
using ComponentKind = AIStudio.Tools.Components;
using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingAssistant</c> for the visual briefing feature.
/// </summary>
public partial class VisualBriefingAssistant : MSGComponentBase
{
    /// <summary>
    /// Defines <c>Store</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private VisualBriefingStore Store { get; init; } = null!;

    /// <summary>
    /// Defines <c>BuildOrchestrator</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private VisualBriefingBuildOrchestrator BuildOrchestrator { get; init; } = null!;

    /// <summary>
    /// Defines <c>BuildProgressService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private VisualBriefingBuildProgressService BuildProgressService { get; init; } = null!;

    /// <summary>
    /// Defines <c>PreviewTokenService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private VisualBriefingPreviewTokenService PreviewTokenService { get; init; } = null!;

    /// <summary>
    /// Defines <c>RustService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private RustService RustService { get; init; } = null!;

    /// <summary>
    /// Defines <c>MediaTranscriptionService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    /// <summary>
    /// Defines <c>DialogService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    /// <summary>
    /// Defines <c>Snackbar</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;

    /// <summary>
    /// Defines <c>AssistantSessionService</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private AssistantSessionService AssistantSessionService { get; init; } = null!;

    /// <summary>
    /// Defines <c>NavigationManager</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    /// <summary>
    /// Defines <c>Logger</c> for the visual briefing feature.
    /// </summary>
    [Inject]
    private ILogger<VisualBriefingAssistant> Logger { get; init; } = null!;

    /// <summary>Tracks briefing projects with an active generation.</summary>
    private readonly HashSet<Guid> generatingBriefings = [];

    /// <summary>Stops the background source-status monitor.</summary>
    private readonly CancellationTokenSource sourceMonitorCancellation = new();

    /// <summary>Stores available and recoverable projects ordered by most recent modification.</summary>
    private IReadOnlyList<VisualBriefingProjectEntry> projects = [];

    /// <summary>Stores the project entry currently selected in the list.</summary>
    private VisualBriefingProjectEntry? selectedProject;

    /// <summary>Stores the project currently displayed by the editor.</summary>
    private VisualBriefingManifest? selectedBriefing;

    /// <summary>Stores source-material attachments for the selected project.</summary>
    private HashSet<FileAttachment> sourceMaterial = [];

    /// <summary>Stores visible visual-asset attachments for the selected project.</summary>
    private HashSet<FileAttachment> visualAssets = [];

    /// <summary>Stores the editable project name.</summary>
    private string projectName = string.Empty;

    /// <summary>Stores the optional author.</summary>
    private string author = string.Empty;

    /// <summary>Stores the current scope or change instruction.</summary>
    private string instruction = string.Empty;

    /// <summary>Stores the selected provider and model.</summary>
    private ProviderSettings provider = ProviderSettings.NONE;

    /// <summary>Stores the selected profile.</summary>
    private Profile profile = Profile.NO_PROFILE;

    /// <summary>Stores the selected target language.</summary>
    private CommonLanguages targetLanguage = CommonLanguages.EN_US;

    /// <summary>Stores a free-form target language.</summary>
    private string customTargetLanguage = string.Empty;

    /// <summary>Stores the audience profile.</summary>
    private AudienceProfile audienceProfile;

    /// <summary>Stores the audience age group.</summary>
    private AudienceAgeGroup audienceAgeGroup;

    /// <summary>Stores the audience organizational level.</summary>
    private AudienceOrganizationalLevel audienceOrganizationalLevel;

    /// <summary>Stores the audience expertise.</summary>
    private AudienceExpertise audienceExpertise;

    /// <summary>Stores whether visible source references are requested.</summary>
    private bool showSourceReferences = true;

    /// <summary>Stores whether large visual assets are optimized.</summary>
    private bool optimizeImages = true;

    /// <summary>Stores the selected protection level.</summary>
    private VisualBriefingProtectionLevel protectionLevel = VisualBriefingProtectionLevel.INTERNAL;

    /// <summary>Stores the free-form protection level.</summary>
    private string customProtectionLevel = string.Empty;

    /// <summary>Stores the selected immutable revision.</summary>
    private Guid selectedRevisionId;

    /// <summary>Stores the preview viewport preset.</summary>
    private VisualBriefingPreviewDevice previewDevice = VisualBriefingPreviewDevice.DESKTOP;

    /// <summary>Stores the current tokenized preview URL.</summary>
    private string previewUrl = string.Empty;

    /// <summary>Stores the last auto-saved UI fingerprint.</summary>
    private string lastPersistedState = string.Empty;

    /// <summary>Stores clipboard-safe diagnostics for the latest operation.</summary>
    private VisualBriefingOperationDiagnostics? lastBuildDiagnostics;

    /// <summary>Stores the latest persistent or live build shown in the stepper.</summary>
    private VisualBriefingBuildRecord? latestBuild;

    /// <summary>Stores incompatible validated content offered for rebuild continuation.</summary>
    private Guid? reusableContentBuildId;

    /// <summary>Owns MudBlazor validation for the selected briefing editor.</summary>
    private MudForm? visualBriefingForm;

    /// <summary>Stores whether all MudBlazor fields in the editor are currently valid.</summary>
    private bool formIsValid;

    /// <summary>Stores the current MudBlazor validation messages.</summary>
    private string[] formIssues = [];

    /// <summary>Requests validation after conditional form controls have rendered.</summary>
    private bool formValidationPending;

    /// <summary>Stores whether this component instance has already left the renderer.</summary>
    private bool isDisposed;

    /// <summary>
    /// Defines <c>IsCurrentBusy</c> for the visual briefing feature.
    /// </summary>
    private bool IsCurrentBusy => this.selectedBriefing is not null &&
                                  (this.IsGenerating(this.selectedBriefing.BriefingId) ||
                                   this.MediaTranscriptionService.IsBusy(this.CurrentMediaOwner));

    /// <summary>
    /// Defines <c>OnInitializedAsync</c> for the visual briefing feature.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!this.SettingsManager.IsAssistantVisible(
                ComponentKind.VISUAL_BRIEFING_ASSISTANT,
                assistantName: T("Visual Briefing Assistant"),
                requiredPreviewFeature: ComponentKind.VISUAL_BRIEFING_ASSISTANT.RequiredPreviewFeature()))
        {
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }

        this.ApplyFilters([], [Event.SEND_TO_VISUAL_BRIEFING_ASSISTANT, Event.CONFIGURATION_CHANGED]);
        this.MediaTranscriptionService.StateChanged += this.MediaStateChanged;
        this.BuildProgressService.Changed += this.BuildProgressChanged;
        await this.ReloadListAsync();
        _ = this.MonitorSourceStatusAsync(this.sourceMonitorCancellation.Token);
        var deferredInstruction = this.MessageBus.CheckDeferredMessages<string>(Event.SEND_TO_VISUAL_BRIEFING_ASSISTANT).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(deferredInstruction))
        {
            if (this.selectedBriefing is null)
                await this.CreateBriefingAsync();

            this.instruction = deferredInstruction;
            await this.SaveCurrentAsync();
        }

        await this.ResumeSelectedBuildAsync();
    }

    /// <summary>
    /// Defines <c>DisposeResources</c> for the visual briefing feature.
    /// </summary>
    protected override void DisposeResources()
    {
        this.isDisposed = true;
        this.sourceMonitorCancellation.Cancel();
        this.sourceMonitorCancellation.Dispose();
        this.MediaTranscriptionService.StateChanged -= this.MediaStateChanged;
        this.BuildProgressService.Changed -= this.BuildProgressChanged;
        base.DisposeResources();
    }

    /// <summary>
    /// Defines <c>OnAfterRenderAsync</c> for the visual briefing feature.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (this.formValidationPending && this.visualBriefingForm is not null)
        {
            this.formValidationPending = false;
            await this.visualBriefingForm.Validate();
        }

        if (this.selectedBriefing is null || this.IsCurrentBusy)
            return;

        var currentState = this.BuildPersistenceFingerprint();
        if (string.Equals(currentState, this.lastPersistedState, StringComparison.Ordinal))
            return;

        this.lastPersistedState = currentState;
        try
        {
            await this.SaveCurrentAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            this.lastPersistedState = string.Empty;
            this.Logger.LogWarning(
                "Could not auto-save visual briefing. BriefingId={BriefingId} ExceptionType={ExceptionType}",
                this.selectedBriefing.BriefingId,
                exception.GetType().Name);
            this.Snackbar.Add(T("The visual briefing settings could not be saved."), Severity.Error);
        }
    }

    /// <summary>
    /// Defines <c>T</c> for the visual briefing feature.
    /// </summary>
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.SEND_TO_VISUAL_BRIEFING_ASSISTANT && data is string text)
        {
            if (this.selectedBriefing is null)
                await this.CreateBriefingAsync();

            this.instruction = text;
            await this.SaveCurrentAsync();
            this.StateHasChanged();
            return;
        }

        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
            this.StateHasChanged();

        await base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    /// <summary>
    /// Defines <c>ConfirmLargeFileAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task<bool> ConfirmLargeFileAsync(string path, string operation)
    {
        if (new FileInfo(path).Length < 50L * 1_024 * 1_024)
            return true;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, string.Format(T("This briefing is larger than 50 MB. Continue with the {0}?"), operation) },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Large visual briefing"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        return result is not null && !result.Canceled;
    }

    /// <summary>
    /// Defines <c>PathComparer</c> for the visual briefing feature.
    /// </summary>
    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}