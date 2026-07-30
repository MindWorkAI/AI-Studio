namespace AIStudio.Assistants.VisualBriefing;

internal sealed partial class VisualBriefingBuildOrchestrator
{
    /// <summary>
    /// Marks an intentionally reused stage as skipped.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="stage">The stage.</param>
    /// <param name="outputHash">The reused output hash.</param>
    private static void MarkSkipped(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStage stage,
        string outputHash)
    {
        var record = GetStage(build, stage);
        record.Status = VisualBriefingBuildStageStatus.SKIPPED;
        record.StartedAtUtc ??= DateTimeOffset.UtcNow;
        record.FinishedAtUtc = DateTimeOffset.UtcNow;
        record.InputFingerprint = outputHash;
        record.OutputHash = outputHash;
        record.Failure = null;
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or creates one stage record.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="stage">The desired stage.</param>
    /// <returns>The stage record.</returns>
    private static VisualBriefingBuildStageRecord GetStage(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStage stage)
    {
        var record = build.Stages.FirstOrDefault(candidate => candidate.Stage == stage);
        if (record is not null)
            return record;
        record = new() { Stage = stage };
        build.Stages.Add(record);
        return record;
    }

    /// <summary>
    /// Persists a terminal build failure.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="status">The terminal status.</param>
    /// <param name="failure">The safe failure.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task SaveTerminalStateAsync(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStatus status,
        VisualBriefingFailure failure,
        CancellationToken token)
    {
        var stage = GetStage(build, failure.Stage);
        var terminalStageStatus = status is VisualBriefingBuildStatus.CANCELED
            ? VisualBriefingBuildStageStatus.CANCELED
            : VisualBriefingBuildStageStatus.FAILED;
        foreach (var runningStage in build.Stages.Where(item =>
                     item.Status is VisualBriefingBuildStageStatus.RUNNING))
        {
            runningStage.Status = terminalStageStatus;
            runningStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            runningStage.Failure = failure;
        }
        if (stage.Status is not (VisualBriefingBuildStageStatus.COMPLETED or VisualBriefingBuildStageStatus.SKIPPED))
        {
            stage.Status = terminalStageStatus;
            stage.StartedAtUtc ??= DateTimeOffset.UtcNow;
            stage.FinishedAtUtc = DateTimeOffset.UtcNow;
            stage.Failure = failure;
        }
        build.Status = status;
        build.Failure = failure;
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await this.store.SaveBuildAsync(build, token);
        this.progressService.Publish(build);
    }

    /// <summary>
    /// Finishes diagnostics and creates a failed result.
    /// </summary>
    /// <param name="diagnostics">The operation diagnostics.</param>
    /// <param name="build">The optional persisted build.</param>
    /// <param name="failure">The safe failure.</param>
    /// <param name="canContinueAsRebuild">Whether content can continue as a rebuild.</param>
    /// <returns>The failed result.</returns>
    private static VisualBriefingBuildResult FinishFailure(
        VisualBriefingOperationDiagnostics diagnostics,
        VisualBriefingBuildRecord? build,
        VisualBriefingFailure failure,
        bool canContinueAsRebuild)
    {
        diagnostics.BuildId = build?.BuildId ?? diagnostics.BuildId;
        diagnostics.Stage = failure.Stage;
        diagnostics.FailureCode = failure.Code;
        diagnostics.ValidationRule = failure.ValidationRule;
        diagnostics.StructuredResponse = failure.StructuredResponse;
        diagnostics.FinishedAtUtc = DateTimeOffset.UtcNow;
        return new(
            false,
            null,
            failure.UserMessage,
            failure.Code,
            diagnostics,
            canContinueAsRebuild);
    }

    /// <summary>
    /// Creates a logging event from a stable identifier.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <returns>The logging event.</returns>
    private static EventId Event(VisualBriefingLogEventId eventId) => new((int)eventId, eventId.ToString());
}
