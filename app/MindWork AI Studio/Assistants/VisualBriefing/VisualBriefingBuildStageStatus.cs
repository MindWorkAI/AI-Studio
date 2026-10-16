using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes the persisted state of one build stage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingBuildStageStatus>))]
public enum VisualBriefingBuildStageStatus
{
    /// <summary>
    /// The stage has not started.
    /// </summary>
    NOT_STARTED,

    /// <summary>
    /// The stage is currently running.
    /// </summary>
    RUNNING,

    /// <summary>
    /// The stage completed successfully.
    /// </summary>
    COMPLETED,

    /// <summary>
    /// The stage failed and may be resumed when its inputs still match.
    /// </summary>
    FAILED,

    /// <summary>
    /// The stage was intentionally skipped because an immutable artifact was reused.
    /// </summary>
    SKIPPED,

    /// <summary>
    /// The stage was canceled before it completed.
    /// </summary>
    CANCELED,
}