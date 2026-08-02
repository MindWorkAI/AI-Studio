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
        this.provider == ProviderSettings.NONE ||
        string.IsNullOrWhiteSpace(this.projectName) ||
        this.targetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(this.customTargetLanguage) ||
        this.protectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(this.customProtectionLevel) ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN && !this.HasSourceMaterial ||
        mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT &&
        !this.SelectedVersionSupportsEdits ||
        mode is not VisualBriefingEditMode.CHANGE_DESIGN &&
        this.selectedBriefing?.Sources.Any(source =>
            source.Status is VisualBriefingSourceStatus.UNREACHABLE or VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED) == true;

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
    private string BuildProgressTitle
    {
        get
        {
            var title = this.latestBuild?.Status switch
            {
                VisualBriefingBuildStatus.COMPLETED => $"{T("Build progress")} · {T("Completed")}",
                VisualBriefingBuildStatus.FAILED => $"{T("Build progress")} · {T("Failed")}",
                VisualBriefingBuildStatus.CANCELED => $"{T("Build progress")} · {T("Canceled")}",
                VisualBriefingBuildStatus.AWAITING_REBUILD => $"{T("Build progress")} · {T("Action required")}",

                _ => $"{T("Build progress")} · {T("Running")}",
            };

            var duration = this.CalculateBuildDuration(this.latestBuild?.Stages ?? []);
            return duration > TimeSpan.Zero ? $"{title} · {FormatBuildDuration(duration)}" : title;
        }
    }

    /// <summary>
    /// Keeps the status stepper informational while allowing actions inside the active step.
    /// </summary>
    private static Task PreventBuildStepperInteractionAsync(StepperInteractionEventArgs args)
    {
        args.Cancel = true;
        return Task.CompletedTask;
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

        if (this.provider == ProviderSettings.NONE)
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
            this.buildDurationReferenceUtc = DateTimeOffset.UtcNow;
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Refreshes live build durations at most once per second while a selected stage is running.
    /// </summary>
    private async Task MonitorBuildDurationAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await this.InvokeAsync(() =>
                {
                    if (this.latestBuild?.Stages.Any(stage => stage.Status is VisualBriefingBuildStageStatus.RUNNING) != true)
                        return;

                    this.buildDurationReferenceUtc = DateTimeOffset.UtcNow;
                    this.StateHasChanged();
                });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
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

        var duration = this.CalculateBuildDuration(records);
        return duration > TimeSpan.Zero ? $"{status} · {FormatBuildDuration(duration)}" : status;
    }

    /// <summary>
    /// Calculates active processing time without counting reused stages or time between resume attempts.
    /// </summary>
    private TimeSpan CalculateBuildDuration(IEnumerable<VisualBriefingBuildStageRecord> records) => records
            .Where(record => record.StartedAtUtc is not null && record.Status is not VisualBriefingBuildStageStatus.SKIPPED)
            .Aggregate(TimeSpan.Zero, (total, record) => total + this.CalculateStageDuration(record));

    /// <summary>
    /// Calculates one stage duration against the shared live timestamp.
    /// </summary>
    private TimeSpan CalculateStageDuration(VisualBriefingBuildStageRecord record)
    {
        var finishedAtUtc = record.Status is VisualBriefingBuildStageStatus.RUNNING ? this.buildDurationReferenceUtc : record.FinishedAtUtc;
        if (record.StartedAtUtc is null || finishedAtUtc is null)
            return TimeSpan.Zero;

        var duration = finishedAtUtc.Value - record.StartedAtUtc.Value;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    /// <summary>
    /// Formats a build duration using the current UI culture.
    /// </summary>
    private static string FormatBuildDuration(TimeSpan duration) => $"{duration.TotalSeconds:0.0} s";

    /// <summary>
    /// Gets the safe failure reason for a UI group.
    /// </summary>
    private string BuildGroupFailure(int index) =>
        BuildStageGroups()[index]
            .Select(stage => this.latestBuild?.Stages.FirstOrDefault(item => item.Stage == stage)?.Failure)
            .FirstOrDefault(failure => failure is not null)?.UserMessage ?? this.latestBuild?.Failure?.UserMessage ?? string.Empty;

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