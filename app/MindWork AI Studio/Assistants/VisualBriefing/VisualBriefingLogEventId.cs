namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stable structured logging event identifiers for the visual briefing subsystem.
/// </summary>
public enum VisualBriefingLogEventId
{
    /// <summary>
    /// A build started.
    /// </summary>
    BUILD_STARTED = 4100,

    /// <summary>
    /// A persisted build resumed.
    /// </summary>
    BUILD_RESUMED = 4101,

    /// <summary>
    /// A stale build was superseded.
    /// </summary>
    BUILD_SUPERSEDED = 4102,

    /// <summary>
    /// A build reached a terminal state.
    /// </summary>
    BUILD_FINISHED = 4103,

    /// <summary>
    /// Source preparation started.
    /// </summary>
    SOURCE_PREPARATION_STARTED = 4110,

    /// <summary>
    /// Source preparation finished.
    /// </summary>
    SOURCE_PREPARATION_FINISHED = 4111,

    /// <summary>
    /// Media or source preparation was rejected.
    /// </summary>
    SOURCE_PREPARATION_REJECTED = 4112,

    /// <summary>
    /// A structured-agent call started.
    /// </summary>
    STRUCTURED_CALL_STARTED = 4120,

    /// <summary>
    /// A structured-agent call finished.
    /// </summary>
    STRUCTURED_CALL_FINISHED = 4121,

    /// <summary>
    /// A design-agent call started.
    /// </summary>
    DESIGN_CALL_STARTED = 4130,

    /// <summary>
    /// A design-agent call finished.
    /// </summary>
    DESIGN_CALL_FINISHED = 4131,

    /// <summary>
    /// A structured response was rejected by parsing or validation.
    /// </summary>
    VALIDATION_REJECTED = 4140,

    /// <summary>
    /// The single automatic repair attempt started.
    /// </summary>
    REPAIR_STARTED = 4141,

    /// <summary>
    /// The automatic repair attempt finished.
    /// </summary>
    REPAIR_FINISHED = 4142,

    /// <summary>
    /// Deterministic assembly started.
    /// </summary>
    ASSEMBLY_STARTED = 4150,

    /// <summary>
    /// Deterministic assembly finished.
    /// </summary>
    ASSEMBLY_FINISHED = 4151,

    /// <summary>
    /// An immutable revision was committed.
    /// </summary>
    REVISION_COMMITTED = 4152,

    /// <summary>
    /// Store initialization or reconciliation ran.
    /// </summary>
    STORE_RECOVERY = 4160,

    /// <summary>
    /// A store write or lock operation failed.
    /// </summary>
    STORE_REJECTED = 4161,

    /// <summary>
    /// A briefing import started or finished.
    /// </summary>
    IMPORT = 4170,

    /// <summary>
    /// A briefing export started or finished.
    /// </summary>
    EXPORT = 4171,

    /// <summary>
    /// A preview request was rejected.
    /// </summary>
    PREVIEW_REJECTED = 4180,

    /// <summary>
    /// A security validation rejected an artifact.
    /// </summary>
    SECURITY_REJECTED = 4181,
}