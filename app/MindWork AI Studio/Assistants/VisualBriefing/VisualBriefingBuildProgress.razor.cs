using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Renders the staged progress, durations, and failures of one visual briefing build.
/// </summary>
/// <remarks>
/// The component derives everything it shows from <see cref="Build"/> alone. It also owns the timer
/// that keeps the duration of a running stage current, so a build in progress re-renders this panel
/// once per second instead of the entire assistant page.
/// </remarks>
public partial class VisualBriefingBuildProgress : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the build whose progress is displayed.
    /// </summary>
    [Parameter, EditorRequired]
    public VisualBriefingBuildRecord? Build { get; set; }

    /// <summary>
    /// Gets or sets whether the resume action is blocked because other work is running.
    /// </summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>
    /// Gets or sets the callback raised when the user resumes a failed or canceled build.
    /// </summary>
    [Parameter]
    public EventCallback OnResume { get; set; }

    /// <summary>
    /// The six UI groups covering the eight durable build stages.
    /// </summary>
    private static readonly VisualBriefingBuildStage[][] STAGE_GROUPS =
    [
        [VisualBriefingBuildStage.SOURCE_PREPARATION],
        [VisualBriefingBuildStage.EVIDENCE],
        [VisualBriefingBuildStage.PLAN],
        [VisualBriefingBuildStage.CONTENT],
        [VisualBriefingBuildStage.DESIGN],
        [VisualBriefingBuildStage.COMPILATION, VisualBriefingBuildStage.ASSEMBLY, VisualBriefingBuildStage.COMMIT],
    ];

    /// <summary>Stops the live build-duration monitor.</summary>
    private readonly CancellationTokenSource durationMonitorCancellation = new();

    /// <summary>Stores the shared timestamp used to render consistent live build durations.</summary>
    private DateTimeOffset durationReferenceUtc = DateTimeOffset.UtcNow;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        _ = this.MonitorBuildDurationAsync(this.durationMonitorCancellation.Token);
    }

    protected override void OnParametersSet()
    {
        // The parent re-renders us whenever it received a progress update, so this is the moment the
        // durations of running stages must be measured against again.
        this.durationReferenceUtc = DateTimeOffset.UtcNow;
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.durationMonitorCancellation.Cancel();
        this.durationMonitorCancellation.Dispose();
        base.DisposeResources();
    }

    #endregion

    /// <summary>
    /// Refreshes live build durations at most once per second while a stage is running.
    /// </summary>
    /// <param name="token">The token that stops the monitor.</param>
    /// <returns>A task that completes once the monitor was stopped.</returns>
    private async Task MonitorBuildDurationAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                // This panel stays on screen for as long as the briefing has any build, so most of the
                // time there is no running stage and nothing to refresh. The check happens here rather
                // than inside the callback below, because otherwise every second would still cost a hop
                // onto the renderer just to find that out. Reading the build here is safe: the progress
                // service publishes snapshots, so this record is never the one the build mutates.
                if (this.Build?.Stages.Any(stage => stage.Status is VisualBriefingBuildStageStatus.RUNNING) != true)
                    continue;

                await this.InvokeAsync(() =>
                {
                    this.durationReferenceUtc = DateTimeOffset.UtcNow;
                    this.StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// Gets the localized title of one build step.
    /// </summary>
    /// <param name="index">The zero-based index of the step.</param>
    /// <returns>The localized step title.</returns>
    private string StepTitle(int index) => index switch
    {
        0 => T("Prepare sources"),
        1 => T("Analyze material"),
        2 => T("Plan briefing"),
        3 => T("Curate content"),
        4 => T("Design presentation"),

        _ => T("Compile and save"),
    };

    /// <summary>Gets the active build stepper index.</summary>
    private int BuildStepperIndex
    {
        get
        {
            for (var index = 0; index < STAGE_GROUPS.Length; index++)
            {
                var statuses = STAGE_GROUPS[index].Select(this.StageStatus).ToArray();
                if (statuses.Any(status => status is VisualBriefingBuildStageStatus.RUNNING or VisualBriefingBuildStageStatus.FAILED or VisualBriefingBuildStageStatus.CANCELED))
                    return index;

                if (statuses.Any(status => status is VisualBriefingBuildStageStatus.NOT_STARTED))
                    return index;
            }

            return STAGE_GROUPS.Length - 1;
        }
    }

    /// <summary>
    /// Gets the localized collapsed build-progress summary.
    /// </summary>
    private string BuildProgressTitle
    {
        get
        {
            if(this.Build is null)
                return $"{T("Build progress")} · {T("Running")}";
            
            var title = this.Build.Status switch
            {
                VisualBriefingBuildStatus.COMPLETED => $"{T("Build progress")} · {T("Completed")}",
                VisualBriefingBuildStatus.FAILED => $"{T("Build progress")} · {T("Failed")}",
                VisualBriefingBuildStatus.CANCELED => $"{T("Build progress")} · {T("Canceled")}",
                VisualBriefingBuildStatus.AWAITING_REBUILD => $"{T("Build progress")} · {T("Action required")}",

                _ => $"{T("Build progress")} · {T("Running")}",
            };

            var duration = this.CalculateBuildDuration(this.Build.Stages);
            return duration > TimeSpan.Zero ? $"{title} · {FormatBuildDuration(duration)}" : title;
        }
    }

    /// <summary>
    /// Gets a persistent stage status, defaulting to not started.
    /// </summary>
    /// <param name="stage">The stage to look up.</param>
    /// <returns>The stage status.</returns>
    private VisualBriefingBuildStageStatus StageStatus(VisualBriefingBuildStage stage) => this.Build?.Stages.FirstOrDefault(item => item.Stage == stage)?.Status ?? VisualBriefingBuildStageStatus.NOT_STARTED;

    /// <summary>
    /// Gets whether one UI group completed or was reused.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns><c>true</c> when the group finished.</returns>
    private bool BuildGroupCompleted(int index) => STAGE_GROUPS[index].All(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.COMPLETED or VisualBriefingBuildStageStatus.SKIPPED);

    /// <summary>
    /// Gets whether one UI group failed.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns><c>true</c> when the group failed.</returns>
    private bool BuildGroupFailed(int index) => STAGE_GROUPS[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.FAILED);

    /// <summary>
    /// Gets whether one UI group was canceled.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns><c>true</c> when the group was canceled.</returns>
    private bool BuildGroupCanceled(int index) => STAGE_GROUPS[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.CANCELED);

    /// <summary>
    /// Gets whether one UI group stopped with a failure or cancellation.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns><c>true</c> when the group stopped.</returns>
    private bool BuildGroupStopped(int index) => this.BuildGroupFailed(index) || this.BuildGroupCanceled(index);

    /// <summary>
    /// Gets whether one UI group is active.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns><c>true</c> when the group is running.</returns>
    private bool BuildGroupRunning(int index) => STAGE_GROUPS[index].Any(stage => this.StageStatus(stage) is VisualBriefingBuildStageStatus.RUNNING);

    /// <summary>
    /// Formats a safe localized status summary and duration.
    /// </summary>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns>The localized summary.</returns>
    private string BuildGroupSummary(int index)
    {
        if(this.Build is null)
            return T("Not started");

        var records = STAGE_GROUPS[index]
            .Select(stage => this.Build.Stages.FirstOrDefault(item => item.Stage == stage))
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
    /// <param name="records">The stage records to aggregate.</param>
    /// <returns>The aggregated duration.</returns>
    private TimeSpan CalculateBuildDuration(IEnumerable<VisualBriefingBuildStageRecord> records) => records
            .Where(record => record.StartedAtUtc is not null && record.Status is not VisualBriefingBuildStageStatus.SKIPPED)
            .Aggregate(TimeSpan.Zero, (total, record) => total + this.CalculateStageDuration(record));

    /// <summary>
    /// Calculates one stage duration against the shared live timestamp.
    /// </summary>
    /// <param name="record">The stage record to measure.</param>
    /// <returns>The stage duration.</returns>
    private TimeSpan CalculateStageDuration(VisualBriefingBuildStageRecord record)
    {
        var finishedAtUtc = record.Status is VisualBriefingBuildStageStatus.RUNNING ? this.durationReferenceUtc : record.FinishedAtUtc;
        if (record.StartedAtUtc is null || finishedAtUtc is null)
            return TimeSpan.Zero;

        var duration = finishedAtUtc.Value - record.StartedAtUtc.Value;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    /// <summary>
    /// Formats a build duration in seconds using the current culture.
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>The formatted duration.</returns>
    private static string FormatBuildDuration(TimeSpan duration) => $"{duration.TotalSeconds:0.0} s";

    /// <summary>
    /// Gets the safe failure reason for a UI group.
    /// </summary>
    /// <remarks>
    /// The recorded issue text of a failure is stable English contract language, because it also goes
    /// back to the model and into the persisted build record. The text shown here is therefore derived
    /// from the stable enums in the current language instead.
    /// </remarks>
    /// <param name="index">The zero-based index of the group.</param>
    /// <returns>The user-facing failure message.</returns>
    private string BuildGroupFailure(int index) => this.Build is null ? string.Empty : STAGE_GROUPS[index]
            .Select(stage => this.Build.Stages.FirstOrDefault(item => item.Stage == stage)?.Failure)
            .FirstOrDefault(failure => failure is not null)?.ToUserMessage() ?? this.Build.Failure?.ToUserMessage() ?? string.Empty;
}