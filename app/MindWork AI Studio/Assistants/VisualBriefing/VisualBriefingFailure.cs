namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Stores safe details about one failed visual briefing operation.
/// </summary>
public sealed class VisualBriefingFailure
{
    /// <summary>
    /// Gets or sets the stable failure code.
    /// </summary>
    public VisualBriefingFailureCode Code { get; set; }

    /// <summary>
    /// Gets or sets the stage that failed.
    /// </summary>
    public VisualBriefingBuildStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the localized or user-safe message.
    /// </summary>
    public string UserMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets technical details that contain no user content.
    /// </summary>
    public string TechnicalDetails { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stable validation rule without user data.
    /// </summary>
    public VisualBriefingValidationRule ValidationRule { get; set; }

    /// <summary>
    /// Gets or sets the safe structured-response diagnostic.
    /// </summary>
    public VisualBriefingStructuredResponseDiagnostic? StructuredResponse { get; set; }
}