using System.Text.Json;

using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

using DialogOptions = AIStudio.Dialogs.DialogOptions;
using ComponentKind = AIStudio.Tools.Components;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>
    /// Defines <c>MinimumProviderConfidence</c> for the visual briefing feature.
    /// </summary>
    private ConfidenceLevel MinimumProviderConfidence => this.SettingsManager.ConfigurationData.VisualBriefing.MinimumProviderConfidence;

    /// <summary>
    /// Defines <c>ReloadListAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ReloadListAsync(Guid? selectId = null)
    {
        this.projects = await this.Store.ListProjectsAsync();
        var id = selectId ??
                 this.selectedProject?.BriefingId ??
                 this.Store.LastSelectedBriefingId ??
                 this.projects.FirstOrDefault()?.BriefingId;

        var selected = id is null
            ? null
            : this.projects.FirstOrDefault(project => project.BriefingId == id);

        selected ??= this.projects.FirstOrDefault();
        if (selected is not null)
            await this.ApplySelectedProjectAsync(selected);
        else
            this.ClearSelectedProject();
    }

    /// <summary>
    /// Defines <c>SelectBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SelectBriefingAsync(Guid briefingId)
    {
        if (this.selectedProject?.BriefingId == briefingId)
            return;

        if (this.selectedBriefing is not null)
            await this.SaveCurrentAsync();

        var project = this.projects.FirstOrDefault(candidate => candidate.BriefingId == briefingId);
        if (project is not null)
            await this.ApplySelectedProjectAsync(project);
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
            { dialog => dialog.UserInput, this.editor.Name },
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
        if (this.selectedProject is null)
            return;

        var parameters = new DialogParameters<ConfirmDialog>();
        if (this.selectedProject.IsAvailable)
            parameters.Add(dialog => dialog.Message, string.Format(T("Permanently delete the visual briefing '{0}' and all of its versions and transcripts?"), this.selectedProject.Name));
        else
        {
            var reportingWarning = T("This visual briefing cannot currently be opened. Consider reporting the problem in the [MindWork AI Studio issue tracker](https://github.com/MindWorkAI/AI-Studio), because a future update may make the briefing accessible again.");
            var deletionWarning = T("Permanently delete this visual briefing and all of its versions and transcripts?");
            parameters.Add(dialog => dialog.MarkdownBody, $"{reportingWarning}\n\n{deletionWarning}");
        }

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete visual briefing permanently"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        var id = this.selectedProject.BriefingId;
        this.MediaTranscriptionService.ClearOwnerState(MediaImportOwner.ForVisualBriefing(id));
        await this.Store.DeleteAsync(id);
        await this.Store.ForgetSelectionAsync(id);
        this.ClearSelectedProject();

        await this.ReloadListAsync();
    }

    /// <summary>
    /// Opens the selected project directory without attempting to read or repair its contents.
    /// </summary>
    private async Task OpenSelectedProjectDirectoryAsync()
    {
        if (this.selectedProject is null)
            return;

        var path = await this.Store.GetProjectDirectoryPathAsync(this.selectedProject.BriefingId);
        if (string.IsNullOrWhiteSpace(path))
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Folder, T("The visual briefing project folder is not available.")));
            return;
        }

        OpenPathResponse response;
        try
        {
            response = await this.RustService.TryOpenPathInRuntimeFileManager(path);
        }
        catch (Exception exception)
        {
            this.Logger.LogWarning(exception, "Could not open the visual briefing project folder. BriefingId={BriefingId}", this.selectedProject.BriefingId);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, T("Could not open the visual briefing project folder.")));
            return;
        }

        if (response.Success)
        {
            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Folder, T("Opened the visual briefing project folder.")));
            return;
        }

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, string.Format(T("Could not open the visual briefing project folder: {0}"), issue)));
    }

    /// <summary>
    /// Defines <c>SaveCurrentAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task SaveCurrentAsync(bool reload = false)
    {
        if (this.selectedBriefing is null || string.IsNullOrWhiteSpace(this.editor.Name))
            return;

        await this.Store.SaveProjectAsync(
            this.selectedBriefing.BriefingId,
            this.editor.Name,
            this.editor.Author,
            this.editor.ToSettings(),
            this.editor.ToSources());

        this.lastPersistedState = this.BuildPersistenceFingerprint();

        if (reload)
            await this.ReloadListAsync(this.selectedBriefing.BriefingId);
        else
            await this.RefreshSavedBriefingAsync(this.selectedBriefing.BriefingId);
    }

    /// <summary>
    /// Refreshes the in-memory manifest copies of one briefing after it was written to disk.
    /// </summary>
    /// <remarks>
    /// The store re-reads and rewrites the manifest file, so the copies this component holds are
    /// stale after every save. They must be refreshed, because selecting a briefing restores the
    /// editor from the stored manifest: a stale copy would first show the values from before the
    /// save and would then be written back over the saved ones on the next save.
    /// The list order is deliberately left untouched. Auto-saving happens while the user is typing,
    /// and re-sorting by modification date would make the edited briefing jump within the list on
    /// every change. Explicit actions re-sort through ReloadListAsync instead.
    /// </remarks>
    /// <param name="briefingId">The briefing that was just saved.</param>
    /// <returns>A task that completes once the in-memory copies match the stored manifest.</returns>
    private async Task RefreshSavedBriefingAsync(Guid briefingId)
    {
        var saved = await this.Store.LoadAsync(briefingId);
        if (saved is null)
            return;

        if (this.selectedBriefing?.BriefingId == briefingId)
            this.selectedBriefing = saved;

        var refreshed = VisualBriefingProjectEntry.FromManifest(saved);
        this.projects = [.. this.projects.Select(project => project.BriefingId == briefingId ? refreshed : project)];

        if (this.selectedProject?.BriefingId == briefingId)
            this.selectedProject = refreshed;
    }

    /// <summary>
    /// Defines <c>ApplySelectedBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ApplySelectedBriefingAsync(VisualBriefingManifest briefing)
    {
        await this.Store.RememberSelectionAsync(briefing.BriefingId);
        this.selectedProject = VisualBriefingProjectEntry.FromManifest(briefing);
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

        this.editor = VisualBriefingEditorState.FromManifest(briefing, this.SettingsManager);

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
        this.formIssues = [];
        this.formValidationPending = true;
    }

    /// <summary>
    /// Applies either a normal editor project or a content-free recovery entry.
    /// </summary>
    private async Task ApplySelectedProjectAsync(VisualBriefingProjectEntry project)
    {
        if (project.IsAvailable)
        {
            await this.ApplySelectedBriefingAsync(project.Manifest!);
            return;
        }

        await this.Store.RememberSelectionAsync(project.BriefingId);
        this.ClearSelectedProject();
        this.selectedProject = project;
    }

    /// <summary>
    /// Clears editor-only state so an unavailable project cannot trigger saves or background work.
    /// </summary>
    private void ClearSelectedProject()
    {
        this.selectedProject = null;
        this.selectedBriefing = null;
        this.editor = new();
        this.selectedRevisionId = Guid.Empty;
        this.previewUrl = string.Empty;
        this.latestBuild = null;
        this.lastBuildDiagnostics = null;
        this.reusableContentBuildId = null;
        this.lastPersistedState = string.Empty;
        this.formIssues = [];
        this.formValidationPending = false;
        this.visualBriefingForm?.ResetValidation();
    }

    /// <summary>
    /// Replaces an available list entry after a background operation updates its manifest.
    /// </summary>
    private void UpdateProject(VisualBriefingManifest briefing)
    {
        var updated = VisualBriefingProjectEntry.FromManifest(briefing);
        this.projects = [.. this.projects.Select(project => project.BriefingId == briefing.BriefingId ? updated : project).OrderByDescending(project => project.ModifiedAtUtc)];

        if (this.selectedProject?.BriefingId == briefing.BriefingId)
            this.selectedProject = updated;
    }

    /// <summary>
    /// Gets a safe list and recovery-view title.
    /// </summary>
    private string ProjectDisplayName(VisualBriefingProjectEntry project)
    {
        if (project.BriefingId == this.selectedBriefing?.BriefingId)
            return this.editor.Name;

        return string.IsNullOrWhiteSpace(project.Name) ? T("Unavailable visual briefing") : project.Name;
    }

    /// <summary>
    /// Gets the concise project-list status.
    /// </summary>
    private string ProjectStatusName(VisualBriefingProjectLoadStatus status) => status switch
    {
        VisualBriefingProjectLoadStatus.NEWER_VERSION => T("Requires a newer AI Studio version"),
        _ => T("Cannot be opened"),
    };

    /// <summary>
    /// Gets the recovery explanation for an unavailable project.
    /// </summary>
    private string ProjectRecoveryMessage(VisualBriefingProjectLoadStatus status) => status switch
    {
        VisualBriefingProjectLoadStatus.NEWER_VERSION => T("This visual briefing was created by a newer AI Studio version and cannot be opened by this version."),
        _ => T("AI Studio cannot read this visual briefing. Its files may be incompatible or damaged."),
    };

    /// <summary>
    /// Defines <c>ProtectionLevelName</c> for the visual briefing feature.
    /// </summary>
    private string ProtectionLevelName(VisualBriefingProtectionLevel level) => level switch
    {
        VisualBriefingProtectionLevel.PUBLIC => T("public"),
        VisualBriefingProtectionLevel.INTERNAL => T("internal"),
        VisualBriefingProtectionLevel.PRIVATE => T("private"),
        VisualBriefingProtectionLevel.CONFIDENTIAL => T("confidential"),
        VisualBriefingProtectionLevel.OTHER => T("other"),

        _ => level.ToString(),
    };

    /// <summary>
    /// Builds the fingerprint that decides whether the editor holds unsaved changes.
    /// </summary>
    /// <remarks>
    /// The fingerprint is serialized from exactly the values that SaveCurrentAsync
    /// hands to the store. That is deliberate: a handwritten field list would silently stop
    /// auto-saving whenever a new setting is added and someone forgets to list it here. Sources are
    /// projected into a named shape because <c>System.Text.Json</c> ignores tuple fields and would
    /// otherwise serialize every source list into the same empty object.
    /// </remarks>
    /// <returns>The fingerprint of the current editor state.</returns>
    private string BuildPersistenceFingerprint() => JsonSerializer.Serialize(
        new
        {
            this.editor.Name,
            this.editor.Author,
            Settings = this.editor.ToSettings(),
            Sources = this.editor.ToSources().Select(source => new { source.Path, source.Kind }).ToArray(),
        }, VisualBriefingJson.Canonical);
}