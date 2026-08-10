using System.Text.Json.Serialization;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stable, machine-readable visual briefing failure codes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VisualBriefingFailureCode>))]
public enum VisualBriefingFailureCode
{
    /// <summary>
    /// No failure occurred.
    /// </summary>
    NONE,

    /// <summary>
    /// The selected provider is unavailable.
    /// </summary>
    PROVIDER_NOT_SELECTED,

    /// <summary>
    /// The selected model lacks a required capability.
    /// </summary>
    MODEL_CAPABILITY_MISSING,

    /// <summary>
    /// A required source cannot be reached.
    /// </summary>
    SOURCE_UNREACHABLE,

    /// <summary>
    /// A media transcript is missing or outdated.
    /// </summary>
    TRANSCRIPT_UNAVAILABLE,

    /// <summary>
    /// Source preparation failed.
    /// </summary>
    SOURCE_PREPARATION_FAILED,

    /// <summary>
    /// A model call failed.
    /// </summary>
    PROVIDER_CALL_FAILED,

    /// <summary>
    /// A model response is not valid JSON.
    /// </summary>
    RESPONSE_JSON_INVALID,

    /// <summary>
    /// A model response does not match its strict contract.
    /// </summary>
    RESPONSE_CONTRACT_INVALID,

    /// <summary>
    /// AI Studio's own compiler produced parts that violate the artifact contract. This is a defect
    /// in AI Studio, never in the model response, and is therefore never sent back to the model.
    /// </summary>
    COMPILER_INVARIANT_VIOLATED,

    /// <summary>
    /// Source coverage is incomplete or duplicated.
    /// </summary>
    SOURCE_COVERAGE_INVALID,

    /// <summary>
    /// A visual asset plan is incomplete or invalid.
    /// </summary>
    ASSET_PLAN_INVALID,

    /// <summary>
    /// An updated content artifact has an incompatible structural signature.
    /// </summary>
    CONTENT_SIGNATURE_INCOMPATIBLE,

    /// <summary>
    /// The presentation violates the declarative artifact contract.
    /// </summary>
    PRESENTATION_INVALID,

    /// <summary>
    /// Deterministic artifact assembly failed.
    /// </summary>
    ASSEMBLY_FAILED,

    /// <summary>
    /// The assembled artifact failed security validation.
    /// </summary>
    ARTIFACT_VALIDATION_FAILED,

    /// <summary>
    /// Atomic persistence or revision commit failed.
    /// </summary>
    STORE_FAILED,

    /// <summary>
    /// The operation produced no material revision changes.
    /// </summary>
    NO_CHANGES,

    /// <summary>
    /// The operation was canceled.
    /// </summary>
    CANCELED,

    /// <summary>
    /// The app stopped while a persistent build stage was running.
    /// </summary>
    BUILD_INTERRUPTED,

    /// <summary>
    /// An unexpected internal error occurred.
    /// </summary>
    UNEXPECTED,
}