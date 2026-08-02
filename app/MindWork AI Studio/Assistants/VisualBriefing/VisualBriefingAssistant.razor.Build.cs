using AIStudio.Tools.AssistantSessions;

using ComponentKind = AIStudio.Tools.Components;
using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>
    /// Gets the active or canceling build session for the selected briefing.
    /// </summary>
    private AssistantSessionSnapshot? CurrentBuildSession => this.selectedBriefing is null ? null : this.AssistantSessionService.TryGetSnapshot(CreateBuildSessionKey(this.selectedBriefing.BriefingId));

    /// <summary>
    /// Gets whether cancellation was already requested for the selected briefing build.
    /// </summary>
    private bool IsCurrentBuildCanceling => this.CurrentBuildSession?.Status is AssistantSessionStatus.CANCELING;

    /// <summary>
    /// Gets whether the selected revision cannot be recompiled without model calls.
    /// </summary>
    private bool CannotRecompile => this.IsCurrentBusy || this.selectedBriefing is null || this.selectedRevisionId == Guid.Empty || !this.SelectedVersionSupportsEdits;

    /// <summary>
    /// Defines <c>CannotGenerate</c> for the visual briefing feature.
    /// </summary>
    private bool CannotGenerate(VisualBriefingEditMode mode) =>
        this.IsCurrentBusy ||
        this.editor.Provider == ProviderSettings.NONE ||
        string.IsNullOrWhiteSpace(this.editor.Name) ||
        this.editor.TargetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(this.editor.CustomTargetLanguage) ||
        this.editor.ProtectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(this.editor.CustomProtectionLevel) ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN && !this.HasSourceMaterial ||
        mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT &&
        !this.SelectedVersionSupportsEdits ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN &&
        this.selectedBriefing?.Sources.Any(source =>
            source.Status is VisualBriefingSourceStatus.UNREACHABLE or VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED) == true;

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

        var generationProvider = this.editor.Provider;
        var generationProfile = this.editor.Profile;

        var sessionKey = CreateBuildSessionKey(briefingId);
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
                terminalStatus = generation.FailureCode is VisualBriefingFailureCode.CANCELED ? AssistantSessionStatus.CANCELED : AssistantSessionStatus.FAILED;
                this.reusableContentBuildId = generation.CanContinueAsRebuild ? generation.Diagnostics.BuildId : null;

                terminalIssue = generation.Issue;
                if (terminalStatus is not AssistantSessionStatus.CANCELED)
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
                    this.UpdateProject(latest);
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

            this.RetireFinishedSession(sessionKey);
            this.generatingBriefings.Remove(briefingId);
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Recompiles the selected immutable revision with the current AI Studio export pipeline.
    /// </summary>
    /// <param name="parentRevisionOverride">An optional parent used while resuming a persisted operation.</param>
    private async Task RecompileAsync(Guid? parentRevisionOverride = null)
    {
        var parentRevisionId = parentRevisionOverride ?? this.selectedRevisionId;
        if (this.selectedBriefing is null ||
            this.IsCurrentBusy ||
            !this.VersionSupportsSemanticEdits(parentRevisionId))
            return;

        var recompileBriefing = this.selectedBriefing;
        var briefingId = recompileBriefing.BriefingId;
        var sessionKey = CreateBuildSessionKey(briefingId);
        if (this.AssistantSessionService.TryGetSnapshot(sessionKey)?.IsActive == true)
            return;

        var cancellation = new CancellationTokenSource();
        var session = await this.AssistantSessionService.TryBeginAsync(
            sessionKey,
            recompileBriefing.Name,
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
            var result = await this.BuildOrchestrator.RecompileAsync(
                recompileBriefing,
                parentRevisionId,
                cancellation.Token);
            
            this.lastBuildDiagnostics = result.Diagnostics;
            this.latestBuild = this.BuildProgressService.GetLatest(briefingId) ?? (await this.Store.ListBuildsAsync(briefingId, cancellation.Token)).FirstOrDefault();

            if (!result.Success || result.Version is null)
            {
                terminalStatus = result.FailureCode is VisualBriefingFailureCode.CANCELED ? AssistantSessionStatus.CANCELED : AssistantSessionStatus.FAILED;
                terminalIssue = result.Issue;
                if (terminalStatus is not AssistantSessionStatus.CANCELED)
                    this.Snackbar.Add(result.Issue, Severity.Error);
                
                return;
            }

            if (this.selectedBriefing?.BriefingId == briefingId)
            {
                await this.ReloadListAsync(briefingId);
                await this.SelectRevisionAsync(result.Version.RevisionId);
            }
            else
            {
                var latest = await this.Store.LoadAsync(briefingId, cancellation.Token);
                if (latest is not null)
                    this.UpdateProject(latest);
            }

            this.Snackbar.Add(T("The briefing was recompiled with the current AI Studio version."), Severity.Success);
            terminalStatus = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            terminalStatus = AssistantSessionStatus.CANCELED;
            terminalIssue = T("The visual briefing recompilation was canceled.");
        }
        catch (Exception exception)
        {
            terminalIssue = T("The visual briefing recompilation failed unexpectedly. Copy the technical details for support.");
            this.Logger.LogError(
                "Unexpected visual briefing UI failure. BriefingId={BriefingId} Mode={Mode} ExceptionType={ExceptionType}",
                briefingId,
                VisualBriefingEditMode.RECOMPILE,
                exception.GetType().Name);
            this.Snackbar.Add(terminalIssue, Severity.Error);
        }
        finally
        {
            await this.AssistantSessionService.CompleteAsync(sessionKey, session.SessionId, terminalStatus, terminalIssue, null, new(StringComparer.Ordinal), this);
            this.RetireFinishedSession(sessionKey);
            this.generatingBriefings.Remove(briefingId);
            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Consumes the finished session of one briefing while this component is still showing it.
    /// </summary>
    /// <remarks>
    /// A briefing session carries no state, because the briefing itself is stored on disk. Its only
    /// remaining purpose after completion is the indicator on the assistant overview. When the user
    /// is still on this page, that indicator would be stale, so we retire the session the same way
    /// <c>AssistantBase</c> does. When the user has navigated away, we keep it so the overview can
    /// report that a background build has finished.
    /// </remarks>
    /// <param name="sessionKey">The session key of the briefing that just finished.</param>
    private void RetireFinishedSession(AssistantSessionKey sessionKey)
    {
        if (!this.isDisposed)
            _ = this.AssistantSessionService.TryTakeInactiveSnapshot(sessionKey);
    }

    /// <summary>
    /// Automatically resumes the selected build that was active when the app stopped.
    /// </summary>
    private async Task ResumeSelectedBuildAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var activeBuild = (await this.Store.ListBuildsAsync(this.selectedBriefing.BriefingId))
            .FirstOrDefault(build => build.Status is VisualBriefingBuildStatus.ACTIVE);

        if (activeBuild is null)
            return;

        if (activeBuild.Mode is VisualBriefingEditMode.RECOMPILE)
        {
            await this.RecompileAsync(activeBuild.ParentRevisionId);
            return;
        }

        if (this.editor.Provider == ProviderSettings.NONE)
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

        _ = this.InvokeAsync(() =>
        {
            if (this.selectedBriefing?.BriefingId != briefingId)
                return;

            this.latestBuild = this.BuildProgressService.GetLatest(briefingId);
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Resumes the latest failed build with its persisted operation inputs.
    /// </summary>
    private async Task ResumeLatestBuildAsync()
    {
        if (this.latestBuild?.Status is not (VisualBriefingBuildStatus.FAILED or VisualBriefingBuildStatus.CANCELED))
            return;

        if (this.latestBuild.Mode is VisualBriefingEditMode.RECOMPILE)
            await this.RecompileAsync(this.latestBuild.ParentRevisionId);
        else
            await this.GenerateAsync(
                this.latestBuild.Mode,
                parentRevisionOverride: this.latestBuild.ParentRevisionId);
    }

    /// <summary>
    /// Requests cancellation for the build running on the selected briefing.
    /// </summary>
    private async Task CancelCurrentBuildAsync()
    {
        if (this.selectedBriefing is null)
            return;

        var sessionKey = CreateBuildSessionKey(this.selectedBriefing.BriefingId);
        if (this.AssistantSessionService.TryGetSnapshot(sessionKey)?.Status is not AssistantSessionStatus.RUNNING)
            return;

        await this.AssistantSessionService.CancelAsync(sessionKey, this);
        this.StateHasChanged();
    }

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
    /// Defines <c>IsGenerating</c> for the visual briefing feature.
    /// </summary>
    private bool IsGenerating(Guid briefingId)
    {
        if (this.generatingBriefings.Contains(briefingId))
            return true;

        return this.AssistantSessionService.TryGetSnapshot(CreateBuildSessionKey(briefingId))?.IsActive == true;
    }

    /// <summary>
    /// Creates the assistant-session key used by a visual briefing build.
    /// </summary>
    private static AssistantSessionKey CreateBuildSessionKey(Guid briefingId) => new(ComponentKind.VISUAL_BRIEFING_ASSISTANT, briefingId.ToString("D"));
}