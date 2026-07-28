using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes the lifecycle state of a persistent visual briefing build.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingBuildStatus>))]
public enum VisualBriefingBuildStatus
{
    /// <summary>
    /// The build is active or can be resumed.
    /// </summary>
    ACTIVE,

    /// <summary>
    /// The build committed an immutable revision.
    /// </summary>
    COMPLETED,

    /// <summary>
    /// The build failed with a safe, persisted failure description.
    /// </summary>
    FAILED,

    /// <summary>
    /// The build was canceled.
    /// </summary>
    CANCELED,

    /// <summary>
    /// The build inputs changed and the build was archived.
    /// </summary>
    SUPERSEDED,

    /// <summary>
    /// A valid content update is structurally incompatible and can continue as a rebuild.
    /// </summary>
    AWAITING_REBUILD,
}