using System.Diagnostics.CodeAnalysis;

using AIStudio.Assistants.SlideBuilder;
using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.AssistantSessions;

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
    
    /// <summary>Stores projects ordered by most recent modification.</summary>
    private IReadOnlyList<VisualBriefingManifest> briefings = [];
    
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

    /// <summary>
    /// Defines <c>CurrentMediaOwner</c> for the visual briefing feature.
    /// </summary>
    private MediaImportOwner CurrentMediaOwner => this.selectedBriefing is null
        ? new(MediaImportOwnerKind.VISUAL_BRIEFING, Guid.Empty.ToString("D"))
        : MediaImportOwner.ForVisualBriefing(this.selectedBriefing.BriefingId);

    /// <summary>
    /// Defines <c>IsCurrentBusy</c> for the visual briefing feature.
    /// </summary>
    private bool IsCurrentBusy => this.selectedBriefing is not null &&
                                  (this.IsGenerating(this.selectedBriefing.BriefingId) ||
                                   this.MediaTranscriptionService.IsBusy(this.CurrentMediaOwner));

    /// <summary>
    /// Defines <c>CannotGenerate</c> for the visual briefing feature.
    /// </summary>
    private bool CannotGenerate(VisualBriefingEditMode mode) =>
        this.IsCurrentBusy ||
        this.provider == ProviderSettings.NONE ||
        string.IsNullOrWhiteSpace(this.projectName) ||
        this.targetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(this.customTargetLanguage) ||
        this.protectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(this.customProtectionLevel) ||
        mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT &&
        !this.SelectedVersionSupportsEdits ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN &&
        this.selectedBriefing?.Sources.Any(source =>
            source.Status is VisualBriefingSourceStatus.UNREACHABLE or VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED) == true;

    /// <summary>
    /// Gets whether the selected revision references all four intermediate artifacts.
    /// </summary>
    private bool SelectedVersionSupportsEdits =>
        this.selectedBriefing?.Versions.FirstOrDefault(version =>
            version.RevisionId == this.selectedRevisionId) is
        {
            EvidenceArtifactId: not null,
            PlanArtifactId: not null,
            ContentArtifactId: not null,
            PresentationArtifactId: not null,
        };

    /// <summary>
    /// Defines <c>MinimumProviderConfidence</c> for the visual briefing feature.
    /// </summary>
    private ConfidenceLevel MinimumProviderConfidence => this.SettingsManager.ConfigurationData.VisualBriefing.MinimumProviderConfidence;

    /// <summary>
    /// Defines <c>CanGoBackward</c> for the visual briefing feature.
    /// </summary>
    private bool CanGoBackward => this.GetSelectedVersionIndex() > 0;

    /// <summary>
    /// Gets whether a newer immutable revision can be selected.
    /// </summary>
    private bool CanGoForward
    {
        get
        {
            var index = this.GetSelectedVersionIndex();
            return index >= 0 && index < (this.selectedBriefing?.Versions.Count ?? 0) - 1;
        }
    }

    /// <summary>
    /// Defines <c>PreviewContainerClass</c> for the visual briefing feature.
    /// </summary>
    private string PreviewContainerClass => $"visual-briefing-preview visual-briefing-preview-{this.previewDevice.ToString().ToLowerInvariant()}";

    /// <summary>Gets the active build stepper index.</summary>
    private int BuildStepperIndex
    {
        get
        {
            if (this.latestBuild is null)
                return 0;
            
            var groups = BuildStageGroups();
            for (var index = 0; index < groups.Length; index++)
            {
                var statuses = groups[index].Select(this.StageStatus).ToArray();
                if (statuses.Any(status => status is VisualBriefingBuildStageStatus.RUNNING or
                        VisualBriefingBuildStageStatus.FAILED or VisualBriefingBuildStageStatus.CANCELED))
                    return index;
                
                if (statuses.Any(status => status is VisualBriefingBuildStageStatus.NOT_STARTED))
                    return index;
            }
            
            return groups.Length - 1;
        }
    }

    /// <summary>
    /// Gets the localized collapsed build-progress summary.
    /// </summary>
    private string BuildProgressTitle => this.latestBuild?.Status switch
    {
        VisualBriefingBuildStatus.COMPLETED => $"{T("Build progress")} · {T("Completed")}",
        VisualBriefingBuildStatus.FAILED => $"{T("Build progress")} · {T("Failed")}",
        VisualBriefingBuildStatus.CANCELED => $"{T("Build progress")} · {T("Canceled")}",
        VisualBriefingBuildStatus.AWAITING_REBUILD => $"{T("Build progress")} · {T("Action required")}",
        
        _ => $"{T("Build progress")} · {T("Running")}",
    };

    /// <summary>
    /// Keeps the status stepper informational while allowing actions inside the active step.
    /// </summary>
    private static Task PreventBuildStepperInteractionAsync(StepperInteractionEventArgs args)
    {
        args.Cancel = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Defines <c>OnInitializedAsync</c> for the visual briefing feature.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!this.SettingsManager.IsAssistantVisible(
                ComponentKind.VISUAL_BRIEFING_ASSISTANT,
                assistantName: T("Visual Briefing Assistant"),
                requiredPreviewFeature: PreviewFeatures.PRE_VISUAL_BRIEFING_ASSISTANT_2026))
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
    /// Defines <c>ReloadListAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ReloadListAsync(Guid? selectId = null)
    {
        this.briefings = await this.Store.ListAsync();
        var id = selectId ??
                 this.selectedBriefing?.BriefingId ??
                 this.Store.LastSelectedBriefingId ??
                 this.briefings.FirstOrDefault()?.BriefingId;
        
        var selected = id is null
            ? null
            : this.briefings.FirstOrDefault(briefing => briefing.BriefingId == id);
        
        selected ??= this.briefings.FirstOrDefault();
        if (selected is not null)
            await this.ApplySelectedBriefingAsync(selected);
    }

    /// <summary>
    /// Defines <c>SelectBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SelectBriefingAsync(Guid briefingId)
    {
        if (this.selectedBriefing?.BriefingId == briefingId)
            return;

        if (this.selectedBriefing is not null)
            await this.SaveCurrentAsync();
        
        var briefing = this.briefings.FirstOrDefault(candidate => candidate.BriefingId == briefingId);
        if (briefing is not null)
            await this.ApplySelectedBriefingAsync(briefing);
    }

    /// <summary>
    /// Defines <c>CreateBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task CreateBriefingAsync()
    {
        var defaults = this.SettingsManager.ConfigurationData.VisualBriefing;
        var defaultProvider = this.SettingsManager.GetPreselectedProvider(ComponentKind.VISUAL_BRIEFING_ASSISTANT);
        var defaultProfile = this.SettingsManager.GetPreselectedProfile(ComponentKind.VISUAL_BRIEFING_ASSISTANT);
        var suggestedName = string.Format(T("Briefing {0}"), DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"));
        var settings = new VisualBriefingLocalSettings
        {
            ProviderId = defaultProvider.Id,
            ModelId = defaultProvider.Model.Id,
            ProfileId = defaultProfile.Id,
            TargetLanguage = defaults.PreselectedTargetLanguage,
            CustomTargetLanguage = defaults.PreselectedOtherLanguage,
            AudienceProfile = defaults.PreselectedAudienceProfile,
            AudienceAgeGroup = defaults.PreselectedAudienceAgeGroup,
            AudienceOrganizationalLevel = defaults.PreselectedAudienceOrganizationalLevel,
            AudienceExpertise = defaults.PreselectedAudienceExpertise,
            ShowSourceReferences = defaults.ShowSourceReferences,
            OptimizeImages = defaults.OptimizeImages,
        };
        
        var briefing = await this.Store.CreateAsync(suggestedName, string.Empty, settings);
        await this.ReloadListAsync(briefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>RenameAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RenameAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var parameters = new DialogParameters<SingleInputDialog>
        {
            { dialog => dialog.Message, T("Enter a new name for this visual briefing.") },
            { dialog => dialog.InputHeaderText, T("Briefing name") },
            { dialog => dialog.UserInput, this.projectName },
            { dialog => dialog.ConfirmText, T("Rename") },
            { dialog => dialog.ConfirmColor, Color.Info },
            { dialog => dialog.AllowEmptyInput, false },
            { dialog => dialog.EmptyInputErrorMessage, T("Please enter a briefing name.") },
        };
        
        var reference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Rename visual briefing"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled || result.Data is not string name)
            return;

        await this.Store.RenameAsync(this.selectedBriefing.BriefingId, name);
        await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>DeleteAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task DeleteAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, string.Format(T("Permanently delete the visual briefing '{0}' and all of its versions and transcripts?"), this.selectedBriefing.Name) },
        };
        
        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete visual briefing permanently"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        var id = this.selectedBriefing.BriefingId;
        this.MediaTranscriptionService.ClearOwnerState(MediaImportOwner.ForVisualBriefing(id));
        await this.Store.DeleteAsync(id);
        await this.Store.ForgetSelectionAsync(id);
        this.selectedBriefing = null;
        this.previewUrl = string.Empty;
        
        await this.ReloadListAsync();
    }

    /// <summary>
    /// Defines <c>SourceMaterialChangedAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SourceMaterialChangedAsync(HashSet<FileAttachment> _)
    {
        var visualPaths = this.visualAssets.Select(attachment => attachment.FilePath).ToHashSet(PathComparer());
        this.sourceMaterial.RemoveWhere(attachment => visualPaths.Contains(attachment.FilePath));
        await this.SaveCurrentAsync(reload: true);
    }

    /// <summary>
    /// Defines <c>VisualAssetsChangedAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task VisualAssetsChangedAsync(HashSet<FileAttachment> _)
    {
        var visualPaths = this.visualAssets.Select(attachment => attachment.FilePath).ToHashSet(PathComparer());
        this.sourceMaterial.RemoveWhere(attachment => visualPaths.Contains(attachment.FilePath));
        await this.SaveCurrentAsync(reload: true);
    }

    /// <summary>
    /// Defines <c>RefreshSourceStatusAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RefreshSourceStatusAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var latest = await this.Store.LoadAsync(this.selectedBriefing.BriefingId);
        if (latest is null)
            return;

        this.selectedBriefing.Sources = latest.Sources;
        this.StateHasChanged();
    }

    /// <summary>
    /// Defines <c>MonitorSourceStatusAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task MonitorSourceStatusAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                if (this.selectedBriefing is not null && !this.IsCurrentBusy)
                    await this.InvokeAsync(this.RefreshSourceStatusAsync);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Defines <c>SaveCurrentAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SaveCurrentAsync(bool reload = false)
    {
        if (this.selectedBriefing is null || string.IsNullOrWhiteSpace(this.projectName))
            return;

        var settings = new VisualBriefingLocalSettings
        {
            ProviderId = this.provider.Id,
            ModelId = this.provider.Model.Id,
            ProfileId = this.profile.Id,
            TargetLanguage = this.targetLanguage,
            CustomTargetLanguage = this.customTargetLanguage,
            AudienceProfile = this.audienceProfile,
            AudienceAgeGroup = this.audienceAgeGroup,
            AudienceOrganizationalLevel = this.audienceOrganizationalLevel,
            AudienceExpertise = this.audienceExpertise,
            ShowSourceReferences = this.showSourceReferences,
            OptimizeImages = this.optimizeImages,
            Instruction = this.instruction,
            ProtectionLevel = this.protectionLevel,
            CustomProtectionLevel = this.customProtectionLevel,
        };
        
        var sources = this.sourceMaterial.Select(attachment => (attachment.FilePath, VisualBriefingSourceKind.SOURCE_MATERIAL))
            .Concat(this.visualAssets.Select(attachment => (attachment.FilePath, VisualBriefingSourceKind.VISUAL_ASSET)));
        
        await this.Store.SaveProjectAsync(
            this.selectedBriefing.BriefingId,
            this.projectName,
            this.author,
            settings,
            sources);
        
        this.lastPersistedState = this.BuildPersistenceFingerprint();

        if (reload)
            await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>GenerateAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task GenerateAsync(
        VisualBriefingEditMode mode,
        Guid? reusableBuildId = null,
        Guid? parentRevisionOverride = null)
    {
        if (this.selectedBriefing is null || this.CannotGenerate(mode))
            return;

        await this.SaveCurrentAsync(reload: true);
        var generationBriefing = this.selectedBriefing;
        var briefingId = generationBriefing.BriefingId;
        var parentRevisionId = parentRevisionOverride ??
                               (generationBriefing.Versions.Count == 0 ? null : this.selectedRevisionId);
        
        var generationProvider = this.provider;
        var generationProfile = this.profile;
        
        var sessionKey = new AssistantSessionKey(ComponentKind.VISUAL_BRIEFING_ASSISTANT, briefingId.ToString("D"));
        if (this.AssistantSessionService.TryGetSnapshot(sessionKey)?.IsActive == true)
            return;

        var cancellation = new CancellationTokenSource();
        var session = await this.AssistantSessionService.TryBeginAsync(
            sessionKey,
            this.selectedBriefing.Name,
            cancellation,
            null,
            new(StringComparer.Ordinal),
            this);
        
        var terminalStatus = AssistantSessionStatus.FAILED;
        var terminalIssue = string.Empty;
        this.generatingBriefings.Add(briefingId);
        this.StateHasChanged();
        try
        {
            var generation = await this.BuildOrchestrator.BuildAsync(
                generationBriefing,
                mode,
                parentRevisionId,
                generationProvider,
                generationProfile,
                reusableBuildId,
                cancellation.Token);
            this.lastBuildDiagnostics = generation.Diagnostics;
            this.latestBuild = this.BuildProgressService.GetLatest(briefingId) ??
                               (await this.Store.ListBuildsAsync(briefingId, cancellation.Token)).FirstOrDefault();
            if (!generation.Success || generation.Version is null)
            {
                this.reusableContentBuildId = generation.CanContinueAsRebuild
                    ? generation.Diagnostics.BuildId
                    : null;
                
                terminalIssue = generation.Issue;
                this.Snackbar.Add(generation.Issue, Severity.Error);
                return;
            }

            this.reusableContentBuildId = null;
            var generatedBriefingIsSelected = this.selectedBriefing?.BriefingId == briefingId;
            if (generatedBriefingIsSelected)
            {
                await this.ReloadListAsync(briefingId);
                await this.SelectRevisionAsync(generation.Version.RevisionId);
            }
            else
            {
                var latest = await this.Store.LoadAsync(briefingId, cancellation.Token);
                if (latest is not null)
                    this.briefings =
                    [
                        .. this.briefings
                            .Select(briefing => briefing.BriefingId == briefingId ? latest : briefing)
                            .OrderByDescending(briefing => briefing.ModifiedAtUtc)
                    ];
            }
            
            this.Snackbar.Add(T("A new visual briefing version was created."), Severity.Success);
            terminalStatus = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            terminalStatus = AssistantSessionStatus.CANCELED;
            terminalIssue = T("The visual briefing generation was canceled.");
        }
        catch (Exception exception)
        {
            terminalIssue = T("The visual briefing operation failed unexpectedly. Copy the technical details for support.");
            this.Logger.LogError(
                "Unexpected visual briefing UI failure. BriefingId={BriefingId} Mode={Mode} ExceptionType={ExceptionType}",
                briefingId,
                mode,
                exception.GetType().Name);
            
            this.Snackbar.Add(terminalIssue, Severity.Error);
        }
        finally
        {
            await this.AssistantSessionService.CompleteAsync(
                sessionKey,
                session.SessionId,
                terminalStatus,
                terminalIssue,
                null,
                new(StringComparer.Ordinal),
                this);
            this.generatingBriefings.Remove(briefingId);
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Automatically resumes the selected build that was active when the app stopped.
    /// </summary>
    private async Task ResumeSelectedBuildAsync()
    {
        if (this.selectedBriefing is null ||
            this.provider == ProviderSettings.NONE)
            return;
        
        var activeBuild = (await this.Store.ListBuildsAsync(this.selectedBriefing.BriefingId))
            .FirstOrDefault(build => build.Status is VisualBriefingBuildStatus.ACTIVE);
        
        if (activeBuild is null)
            return;
        
        await this.GenerateAsync(
            activeBuild.Mode,
            reusableBuildId: null,
            parentRevisionOverride: activeBuild.ParentRevisionId);
    }

    /// <summary>
    /// Applies a content-free live progress update for the selected project.
    /// </summary>
    private void BuildProgressChanged(Guid briefingId)
    {
        if (this.selectedBriefing?.BriefingId != briefingId)
            return;
        
        this.latestBuild = this.BuildProgressService.GetLatest(briefingId);
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    /// <summary>
    /// Resumes the latest failed build with its persisted operation inputs.
    /// </summary>
    private async Task ResumeLatestBuildAsync()
    {
        if (this.latestBuild?.Status is not (VisualBriefingBuildStatus.FAILED or VisualBriefingBuildStatus.CANCELED))
            return;
        
        await this.GenerateAsync(
            this.latestBuild.Mode,
            parentRevisionOverride: this.latestBuild.ParentRevisionId);
    }

    /// <summary>
    /// Gets the six UI groups for the eight durable build stages.
    /// </summary>
    private static VisualBriefingBuildStage[][] BuildStageGroups() =>
    [
        [VisualBriefingBuildStage.SOURCE_PREPARATION],
        [VisualBriefingBuildStage.EVIDENCE],
        [VisualBriefingBuildStage.PLAN],
        [VisualBriefingBuildStage.CONTENT],
        [VisualBriefingBuildStage.DESIGN],
        [VisualBriefingBuildStage.COMPILATION, VisualBriefingBuildStage.ASSEMBLY, VisualBriefingBuildStage.COMMIT],
    ];

    /// <summary>
    /// Gets a persistent stage status, defaulting to not started.
    /// </summary>
    private VisualBriefingBuildStageStatus StageStatus(VisualBriefingBuildStage stage) =>
        this.latestBuild?.Stages.FirstOrDefault(item => item.Stage == stage)?.Status ??
        VisualBriefingBuildStageStatus.NOT_STARTED;

    /// <summary>
    /// Gets whether one UI group completed or was reused.
    /// </summary>
    private bool BuildGroupCompleted(int index) =>
        BuildStageGroups()[index].All(stage =>
            this.StageStatus(stage) is VisualBriefingBuildStageStatus.COMPLETED or VisualBriefingBuildStageStatus.SKIPPED);

    /// <summary>
    /// Gets whether one UI group failed.
    /// </summary>
    private bool BuildGroupFailed(int index) =>
        BuildStageGroups()[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.FAILED);

    /// <summary>
    /// Gets whether one UI group was canceled.
    /// </summary>
    private bool BuildGroupCanceled(int index) =>
        BuildStageGroups()[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.CANCELED);

    /// <summary>
    /// Gets whether one UI group stopped with a failure or cancellation.
    /// </summary>
    private bool BuildGroupStopped(int index) =>
        this.BuildGroupFailed(index) || this.BuildGroupCanceled(index);

    /// <summary>
    /// Gets whether one UI group is active.
    /// </summary>
    private bool BuildGroupRunning(int index) =>
        BuildStageGroups()[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.RUNNING);

    /// <summary>
    /// Formats a safe localized status summary and duration.
    /// </summary>
    private string BuildGroupSummary(int index)
    {
        if (this.latestBuild is null)
            return T("Not started");
        
        var records = BuildStageGroups()[index]
            .Select(stage => this.latestBuild.Stages.FirstOrDefault(item => item.Stage == stage))
            .Where(record => record is not null)
            .Cast<VisualBriefingBuildStageRecord>()
            .ToArray();
        
        var status = this.BuildGroupRunning(index)
            ? T("Running")
            : this.BuildGroupFailed(index)
                ? T("Failed")
                : this.BuildGroupCanceled(index)
                    ? T("Canceled")
                    : records.Length > 0 && records.All(record => record.Status is VisualBriefingBuildStageStatus.SKIPPED)
                        ? T("Reused")
                        : this.BuildGroupCompleted(index)
                            ? T("Completed")
                            : T("Not started");
        
        var duration = records
            .Where(record => record.StartedAtUtc is not null)
            .Aggregate(TimeSpan.Zero, (total, record) =>
                total + ((record.FinishedAtUtc ?? DateTimeOffset.UtcNow) - record.StartedAtUtc!.Value));
        
        return duration > TimeSpan.Zero
            ? $"{status} · {duration.TotalSeconds:0.0} s"
            : status;
    }

    /// <summary>
    /// Gets the safe failure reason for a UI group.
    /// </summary>
    private string BuildGroupFailure(int index) =>
        BuildStageGroups()[index]
            .Select(stage => this.latestBuild?.Stages.FirstOrDefault(item => item.Stage == stage)?.Failure)
            .FirstOrDefault(failure => failure is not null)?.UserMessage ??
        this.latestBuild?.Failure?.UserMessage ??
        string.Empty;

    /// <summary>
    /// Defines <c>CopyTechnicalDetailsAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task CopyTechnicalDetailsAsync()
    {
        if (this.lastBuildDiagnostics is null)
            return;
        
        await this.RustService.CopyText2Clipboard(
            this.Snackbar,
            this.lastBuildDiagnostics.ToClipboardText());
    }

    /// <summary>
    /// Defines <c>SelectRevisionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SelectRevisionAsync(Guid revisionId)
    {
        if (this.selectedBriefing is null ||
            this.selectedBriefing.Versions.All(version => version.RevisionId != revisionId))
            return;

        this.selectedRevisionId = revisionId;
        var token = this.PreviewTokenService.Issue(this.selectedBriefing.BriefingId, revisionId);
        this.previewUrl = $"/visual-briefing/preview/{this.selectedBriefing.BriefingId:D}/{revisionId:D}?token={Uri.EscapeDataString(token)}";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Defines <c>PreviousVersionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task PreviousVersionAsync()
    {
        var versions = this.OrderedVersions();
        var index = this.GetSelectedVersionIndex();
        if (index > 0)
            await this.SelectRevisionAsync(versions[index - 1].RevisionId);
    }

    /// <summary>
    /// Defines <c>NextVersionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task NextVersionAsync()
    {
        var versions = this.OrderedVersions();
        var index = this.GetSelectedVersionIndex();
        if (index >= 0 && index < versions.Count - 1)
            await this.SelectRevisionAsync(versions[index + 1].RevisionId);
    }

    /// <summary>
    /// Defines <c>ExportAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ExportAsync()
    {
        if (this.selectedBriefing is null || this.selectedRevisionId == Guid.Empty)
            return;

        var sourcePath = await this.Store.GetVersionPathAsync(this.selectedBriefing.BriefingId, this.selectedRevisionId);
        if (sourcePath is null)
            return;
        
        if (await this.Store.ReadVersionPartsAsync(this.selectedBriefing.BriefingId, this.selectedRevisionId) is null)
        {
            this.Snackbar.Add(T("The selected briefing version failed validation and cannot be exported."), Severity.Error);
            return;
        }
        
        if (!await this.ConfirmLargeFileAsync(sourcePath, T("export")))
            return;

        var response = await this.RustService.SaveFile(
            T("Export visual briefing"),
            [FileTypes.VISUAL_BRIEFING_HTML],
            $"{SafeFileName(this.selectedBriefing.Name)}.html");
        
        if (response.UserCancelled)
            return;

        if (PathComparer().Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(response.SaveFilePath)))
        {
            this.Snackbar.Add(T("Choose a different export location so the immutable briefing version is not overwritten."), Severity.Error);
            return;
        }

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
        await using var destination = new FileStream(response.SaveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65_536, true);
        await source.CopyToAsync(destination);
        
        var exportedVersion = this.selectedBriefing.Versions.First(version =>
            version.RevisionId == this.selectedRevisionId);
        
        this.Logger.LogInformation(
            new EventId((int)VisualBriefingLogEventId.EXPORT, VisualBriefingLogEventId.EXPORT.ToString()),
            "Visual briefing version exported. OperationId={OperationId} BuildId={BuildId} BriefingId={BriefingId} RevisionId={RevisionId} PayloadHash={PayloadHash} Bytes={Bytes}",
            exportedVersion.OperationId,
            exportedVersion.BuildId,
            this.selectedBriefing.BriefingId,
            exportedVersion.RevisionId,
            exportedVersion.PayloadHash,
            source.Length);
        
        this.Snackbar.Add(T("The visual briefing was exported."), Severity.Success);
    }

    /// <summary>
    /// Defines <c>ImportAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ImportAsync()
    {
        var response = await this.RustService.SelectFile(T("Import visual briefing"), [FileTypes.VISUAL_BRIEFING_HTML]);
        if (response.UserCancelled || !await this.ConfirmLargeFileAsync(response.SelectedFilePath, T("import")))
            return;

        var imported = await this.Store.ImportAsync(response.SelectedFilePath, importNameConflictAsCopy: false);
        if (imported.RequiresCopyConfirmation)
        {
            var parameters = new DialogParameters<ConfirmDialog>
            {
                { dialog => dialog.Message, T("This briefing ID already exists under another name. Import it as a copy with a new ID?") },
            };
            
            var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Import as copy"), parameters, DialogOptions.FULLSCREEN);
            var result = await reference.Result;
            if (result is null || result.Canceled)
                return;
            
            imported = await this.Store.ImportAsync(response.SelectedFilePath, importNameConflictAsCopy: true);
        }

        if (!imported.Success)
        {
            this.Snackbar.Add(imported.Issue, Severity.Error);
            return;
        }

        await this.ReloadListAsync(imported.BriefingId);
        await this.SelectRevisionAsync(imported.RevisionId);
        
        this.Logger.LogInformation(
            new EventId((int)VisualBriefingLogEventId.IMPORT, VisualBriefingLogEventId.IMPORT.ToString()),
            "Visual briefing version imported. BriefingId={BriefingId} RevisionId={RevisionId} Deduplicated={Deduplicated}",
            imported.BriefingId,
            imported.RevisionId,
            imported.WasDeduplicated);
        
        this.Snackbar.Add(imported.WasDeduplicated ? T("This briefing revision was already imported.") : T("The visual briefing was imported."), Severity.Success);
    }

    /// <summary>
    /// Defines <c>RelinkAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RelinkAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null)
            return;
        var response = await this.RustService.SelectFile(T("Relink briefing source"), initialFile: source.Path);
        if (response.UserCancelled)
            return;

        await this.Store.RelinkSourceAsync(this.selectedBriefing.BriefingId, source.SourceId, response.SelectedFilePath);
        await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>RemoveSourceAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RemoveSourceAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null)
            return;
        
        await this.Store.RemoveSourceAsync(this.selectedBriefing.BriefingId, source.SourceId);
        await this.ReloadListAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Defines <c>RetranscribeAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task RetranscribeAsync(VisualBriefingSource source)
    {
        if (this.selectedBriefing is null || !source.IsMedia || !File.Exists(source.Path))
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, T("The media file changed. Transcribe it again with the configured transcription provider?") },
        };
        
        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Transcribe media again"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.MediaTranscriptionService.TryStartAttachmentBatch(
            [source.Path],
            new(this.CurrentMediaOwner, source.SourceId.ToString("D")));
    }

    /// <summary>
    /// Defines <c>ApplySelectedBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private async Task ApplySelectedBriefingAsync(VisualBriefingManifest briefing)
    {
        await this.Store.RememberSelectionAsync(briefing.BriefingId);
        this.selectedBriefing = briefing;
        var resumableBuilds = await this.Store.ListBuildsAsync(briefing.BriefingId);
        var persistedDiagnostics = resumableBuilds.FirstOrDefault() is { } latestPersistedBuild
            ? VisualBriefingOperationDiagnostics.FromBuildRecord(latestPersistedBuild)
            : null;
        
        this.latestBuild = this.BuildProgressService.GetLatest(briefing.BriefingId) ?? resumableBuilds.FirstOrDefault();
        this.lastBuildDiagnostics = this.BuildOrchestrator.GetDiagnostics(briefing.BriefingId) ?? persistedDiagnostics;
        
        this.reusableContentBuildId = resumableBuilds
            .FirstOrDefault(build => build.Status is VisualBriefingBuildStatus.AWAITING_REBUILD)
            ?.BuildId;
        
        this.projectName = briefing.Name;
        this.author = briefing.Author;
        this.instruction = briefing.Settings.Instruction;
        this.targetLanguage = briefing.Settings.TargetLanguage;
        this.customTargetLanguage = briefing.Settings.CustomTargetLanguage;
        this.audienceProfile = briefing.Settings.AudienceProfile;
        this.audienceAgeGroup = briefing.Settings.AudienceAgeGroup;
        this.audienceOrganizationalLevel = briefing.Settings.AudienceOrganizationalLevel;
        this.audienceExpertise = briefing.Settings.AudienceExpertise;
        this.showSourceReferences = briefing.Settings.ShowSourceReferences;
        this.optimizeImages = briefing.Settings.OptimizeImages;
        this.protectionLevel = briefing.Settings.ProtectionLevel;
        this.customProtectionLevel = briefing.Settings.CustomProtectionLevel;
        
        this.provider = this.SettingsManager.ConfigurationData.Providers
            .FirstOrDefault(candidate =>
                candidate.Id == briefing.Settings.ProviderId &&
                candidate.Model.Id == briefing.Settings.ModelId) ?? ProviderSettings.NONE;
        
        this.profile = this.SettingsManager.ConfigurationData.Profiles
            .FirstOrDefault(candidate => candidate.Id == briefing.Settings.ProfileId) ?? Profile.NO_PROFILE;
        
        this.sourceMaterial =
        [
            .. briefing.Sources
                .Where(source => source.Kind is VisualBriefingSourceKind.SOURCE_MATERIAL)
                .Select(source => FileAttachment.FromPath(source.Path))
        ];
        
        this.visualAssets =
        [
            .. briefing.Sources
                .Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
                .Select(source => FileAttachment.FromPath(source.Path))
        ];
        
        var revisionId = briefing.Versions.Any(version => version.RevisionId == this.selectedRevisionId)
            ? this.selectedRevisionId
            : briefing.Versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault()?.RevisionId ?? Guid.Empty;
        
        if (revisionId != Guid.Empty)
            _ = this.SelectRevisionAsync(revisionId);
        else
        {
            this.selectedRevisionId = Guid.Empty;
            this.previewUrl = string.Empty;
        }
        
        this.lastPersistedState = this.BuildPersistenceFingerprint();
    }

    /// <summary>
    /// Defines <c>MediaStateChanged</c> for the visual briefing feature.
    /// </summary>
    private void MediaStateChanged(MediaImportOwner owner)
    {
        if (owner.Kind is not MediaImportOwnerKind.VISUAL_BRIEFING ||
            !Guid.TryParse(owner.Id, out var briefingId))
            return;

        _ = this.InvokeAsync(async () =>
        {
            if (!this.MediaTranscriptionService.IsBusy(owner))
            {
                var latest = await this.Store.LoadAsync(briefingId);
                if (latest is not null)
                {
                    this.briefings =
                    [
                        .. this.briefings
                            .Select(briefing => briefing.BriefingId == briefingId ? latest : briefing)
                            .OrderByDescending(briefing => briefing.ModifiedAtUtc)
                    ];
                    
                    if (this.selectedBriefing?.BriefingId == briefingId)
                        await this.ApplySelectedBriefingAsync(latest);
                }
            }

            this.StateHasChanged();
        });
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
    /// Defines <c>OrderedVersions</c> for the visual briefing feature.
    /// </summary>
    private IReadOnlyList<VisualBriefingVersion> OrderedVersions() =>
        this.selectedBriefing?.Versions.OrderBy(version => version.VersionNumber).ToArray() ?? [];

    /// <summary>
    /// Defines <c>GetSelectedVersionIndex</c> for the visual briefing feature.
    /// </summary>
    private int GetSelectedVersionIndex()
    {
        var versions = this.OrderedVersions();
        for (var index = 0; index < versions.Count; index++)
            if (versions[index].RevisionId == this.selectedRevisionId)
                return index;
        
        return -1;
    }

    /// <summary>
    /// Defines <c>IsGenerating</c> for the visual briefing feature.
    /// </summary>
    private bool IsGenerating(Guid briefingId)
    {
        if (this.generatingBriefings.Contains(briefingId))
            return true;

        var key = new AssistantSessionKey(ComponentKind.VISUAL_BRIEFING_ASSISTANT, briefingId.ToString("D"));
        return this.AssistantSessionService.TryGetSnapshot(key)?.IsActive == true;
    }

    /// <summary>
    /// Defines <c>ProtectionLevelName</c> for the visual briefing feature.
    /// </summary>
    private string ProtectionLevelName(VisualBriefingProtectionLevel level) => level switch
    {
        VisualBriefingProtectionLevel.PUBLIC => T("public"),
        VisualBriefingProtectionLevel.INTERNAL => T("internal"),
        VisualBriefingProtectionLevel.PRIVATE => T("private"),
        VisualBriefingProtectionLevel.CONFIDENTIAL => T("confidential"),
        VisualBriefingProtectionLevel.STRICTLY_CONFIDENTIAL => T("strictly confidential"),
        VisualBriefingProtectionLevel.SECRET => T("secret"),
        VisualBriefingProtectionLevel.TOP_SECRET => T("top secret"),
        VisualBriefingProtectionLevel.OTHER => T("other"),
        
        _ => level.ToString(),
    };

    /// <summary>
    /// Defines <c>SourceStatusName</c> for the visual briefing feature.
    /// </summary>
    private string SourceStatusName(VisualBriefingSourceStatus status) => status switch
    {
        VisualBriefingSourceStatus.UNCHANGED => T("unchanged"),
        VisualBriefingSourceStatus.CHANGED => T("changed"),
        VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED => T("transcript outdated"),
        VisualBriefingSourceStatus.UNREACHABLE => T("unreachable"),
        
        _ => status.ToString(),
    };

    /// <summary>
    /// Defines <c>SourceStatusColor</c> for the visual briefing feature.
    /// </summary>
    private static Color SourceStatusColor(VisualBriefingSourceStatus status) => status switch
    {
        VisualBriefingSourceStatus.UNCHANGED => Color.Success,
        VisualBriefingSourceStatus.CHANGED => Color.Warning,
        VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED => Color.Warning,
        VisualBriefingSourceStatus.UNREACHABLE => Color.Error,
        _ => Color.Default,
    };

    /// <summary>
    /// Defines <c>SafeFileName</c> for the visual briefing feature.
    /// </summary>
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(name) ? "visual-briefing" : name;
    }

    /// <summary>
    /// Defines <c>PathComparer</c> for the visual briefing feature.
    /// </summary>
    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Defines <c>BuildPersistenceFingerprint</c> for the visual briefing feature.
    /// </summary>
    private string BuildPersistenceFingerprint() => string.Join('\u001f',
        this.projectName,
        this.author,
        this.instruction,
        this.provider.Id,
        this.provider.Model.Id,
        this.profile.Id,
        this.targetLanguage,
        this.customTargetLanguage,
        this.audienceProfile,
        this.audienceAgeGroup,
        this.audienceOrganizationalLevel,
        this.audienceExpertise,
        this.showSourceReferences,
        this.optimizeImages,
        this.protectionLevel,
        this.customProtectionLevel,
        string.Join('\u001e', this.sourceMaterial.Select(attachment => attachment.FilePath).Order(StringComparer.Ordinal)),
        string.Join('\u001e', this.visualAssets.Select(attachment => attachment.FilePath).Order(StringComparer.Ordinal)));
}