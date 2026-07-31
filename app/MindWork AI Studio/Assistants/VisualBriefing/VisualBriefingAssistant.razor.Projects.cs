using System.Diagnostics.CodeAnalysis;

using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

using DialogOptions = AIStudio.Dialogs.DialogOptions;
using ComponentKind = AIStudio.Tools.Components;
using ProviderSettings = AIStudio.Settings.Provider;

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
            this.Snackbar.Add(T("The visual briefing project folder is not available."), Severity.Warning);
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
            this.Snackbar.Add(T("Could not open the visual briefing project folder."), Severity.Error);
            return;
        }

        if (response.Success)
        {
            this.Snackbar.Add(T("Opened the visual briefing project folder."), Severity.Success);
            return;
        }

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        this.Snackbar.Add(string.Format(T("Could not open the visual briefing project folder: {0}"), issue), Severity.Error);
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
    /// Defines <c>ApplySelectedBriefingAsync</c> for the visual briefing feature.
    /// </summary>
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
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
        this.sourceMaterial = [];
        this.visualAssets = [];
        this.selectedRevisionId = Guid.Empty;
        this.previewUrl = string.Empty;
        this.latestBuild = null;
        this.lastBuildDiagnostics = null;
        this.reusableContentBuildId = null;
        this.lastPersistedState = string.Empty;
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
            return this.projectName;

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