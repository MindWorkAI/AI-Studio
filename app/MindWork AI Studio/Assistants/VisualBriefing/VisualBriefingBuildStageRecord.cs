namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores durable progress for one build stage.
/// </summary>
public sealed class VisualBriefingBuildStageRecord
{
    /// <summary>
    /// Gets or sets the stage.
    /// </summary>
    public VisualBriefingBuildStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the current stage status.
    /// </summary>
    public VisualBriefingBuildStageStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the input fingerprint used for resume decisions.
    /// </summary>
    public string InputFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time at which the stage started.
    /// </summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the time at which the stage finished.
    /// </summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of model attempts used by the stage.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the validated artifact hash produced by the stage.
    /// </summary>
    public string OutputHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a safe stage failure.
    /// </summary>
    public VisualBriefingFailure? Failure { get; set; }
}