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
    /// Gets whether one edit mode is currently blocked.
    /// </summary>
    /// <remarks>
    /// A mode is blocked by the very issues listed below the buttons, minus the ones that do not apply
    /// to it. Changing only the design rebuilds the presentation from the validated content of a stored
    /// version, so it neither needs source material nor cares whether a source file moved away in the
    /// meantime. The two modes that edit a stored version instead require that version to still carry
    /// its semantic artifacts.
    /// </remarks>
    /// <param name="mode">The edit mode the user asked for.</param>
    /// <returns><c>true</c> when the mode must stay disabled.</returns>
    private bool CannotGenerate(VisualBriefingEditMode mode) =>
        this.IsCurrentBusy ||
        this.selectedBriefing is null ||
        this.FieldIssues.Count > 0 ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN && this.SourceIssues.Count > 0 ||
        mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT && !this.SelectedVersionSupportsEdits;

    /// <summary>
    /// Runs one long-running briefing operation inside the shared session, progress, and error envelope.
    /// </summary>
    /// <remarks>
    /// Generating a new version and recompiling an existing one differ only in the guard, the call they
    /// make, and the messages they show. Everything around that is identical: the per-briefing session,
    /// the busy marker, the diagnostics, the reload of either the editor or the background list entry,
    /// and the terminal status. Keeping that envelope in one place is what makes both paths behave the
    /// same when an operation is canceled or fails unexpectedly.
    /// </remarks>
    /// <param name="briefing">The briefing the operation runs on.</param>
    /// <param name="mode">The edit mode, used for diagnostics.</param>
    /// <param name="operation">The orchestrator call to run.</param>
    /// <param name="successMessage">The message shown after a new version was committed.</param>
    /// <param name="canceledMessage">The issue recorded when the user canceled the operation.</param>
    /// <param name="unexpectedFailureMessage">The issue recorded when the operation threw.</param>
    /// <returns>A task that completes once the operation reached a terminal state.</returns>
    private async Task RunBriefingOperationAsync(VisualBriefingManifest briefing, VisualBriefingEditMode mode, Func<CancellationToken, Task<VisualBriefingBuildResult>> operation,
        string successMessage, string canceledMessage, string unexpectedFailureMessage)
    {
        var briefingId = briefing.BriefingId;
        var sessionKey = CreateBuildSessionKey(briefingId);
        if (this.AssistantSessionService.TryGetSnapshot(sessionKey)?.IsActive == true)
            return;

        // The session service disposes this token source when the session completes:
        var cancellation = new CancellationTokenSource();
        var session = await this.AssistantSessionService.TryBeginAsync(sessionKey, briefing.Name, cancellation, null,
            new(StringComparer.Ordinal), this);

        var terminalStatus = AssistantSessionStatus.FAILED;
        var terminalIssue = string.Empty;
        this.generatingBriefings.Add(briefingId);
        this.StateHasChanged();

        try
        {
            var result = await operation(cancellation.Token);
            this.lastBuildDiagnostics = result.Diagnostics;
            this.latestBuild = this.BuildProgressService.GetLatest(briefingId) ?? (await this.Store.ListBuildsAsync(briefingId, cancellation.Token)).FirstOrDefault();

            if (!result.Success || result.Version is null)
            {
                terminalStatus = result.FailureCode is VisualBriefingFailureCode.CANCELED ? AssistantSessionStatus.CANCELED : AssistantSessionStatus.FAILED;
                this.reusableContentBuildId = result.CanContinueAsRebuild ? result.Diagnostics.BuildId : null;

                terminalIssue = result.Issue;
                if (terminalStatus is not AssistantSessionStatus.CANCELED)
                    await this.MessageBus.SendError(new(Icons.Material.Filled.AutoAwesome, result.Issue));

                return;
            }

            this.reusableContentBuildId = null;
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

            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.AutoAwesome, successMessage));
            terminalStatus = AssistantSessionStatus.COMPLETED;
        }
        catch (OperationCanceledException)
        {
            terminalStatus = AssistantSessionStatus.CANCELED;
            terminalIssue = canceledMessage;
        }
        catch (Exception exception)
        {
            terminalIssue = unexpectedFailureMessage;
            this.Logger.LogError("Unexpected visual briefing UI failure. BriefingId={BriefingId} Mode={Mode} ExceptionType={ExceptionType}", briefingId, mode, exception.GetType().Name);
            await this.MessageBus.SendError(new(Icons.Material.Filled.AutoAwesome, terminalIssue));
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
    /// Generates a new immutable version of the selected briefing.
    /// </summary>
    /// <param name="mode">The edit mode to run.</param>
    /// <param name="reusableBuildId">An optional build whose validated content is reused.</param>
    /// <param name="parentRevisionOverride">An optional parent used while resuming a persisted operation.</param>
    private async Task GenerateAsync(VisualBriefingEditMode mode, Guid? reusableBuildId = null, Guid? parentRevisionOverride = null)
    {
        if (this.selectedBriefing is null || this.CannotGenerate(mode))
            return;

        // Saving reloads the list, which replaces the selected manifest. Everything below must use the
        // reloaded instance, so the briefing is captured only after the save:
        await this.SaveCurrentAsync(reload: true);
        var generationBriefing = this.selectedBriefing;
        var parentRevisionId = parentRevisionOverride ?? (generationBriefing.Versions.Count == 0 ? null : this.selectedRevisionId);
        var generationProvider = this.editor.Provider;
        var generationProfile = this.editor.Profile;

        await this.RunBriefingOperationAsync(generationBriefing, mode, token => this.BuildOrchestrator.BuildAsync(generationBriefing, mode,
                parentRevisionId, generationProvider, generationProfile, reusableBuildId, token),
            T("A new visual briefing version was created."),
            T("The visual briefing generation was canceled."),
            T("The visual briefing operation failed unexpectedly. Copy the technical details for support."));
    }

    /// <summary>
    /// Recompiles the selected immutable revision with the current AI Studio export pipeline.
    /// </summary>
    /// <param name="parentRevisionOverride">An optional parent used while resuming a persisted operation.</param>
    private async Task RecompileAsync(Guid? parentRevisionOverride = null)
    {
        var parentRevisionId = parentRevisionOverride ?? this.selectedRevisionId;
        if (this.selectedBriefing is null || this.IsCurrentBusy || !this.VersionSupportsSemanticEdits(parentRevisionId))
            return;

        var recompileBriefing = this.selectedBriefing;
        await this.RunBriefingOperationAsync(
            recompileBriefing,
            VisualBriefingEditMode.RECOMPILE,
            token => this.BuildOrchestrator.RecompileAsync(recompileBriefing, parentRevisionId, token),
            T("The briefing was recompiled with the current AI Studio version."),
            T("The visual briefing recompilation was canceled."),
            T("The visual briefing recompilation failed unexpectedly. Copy the technical details for support."));
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