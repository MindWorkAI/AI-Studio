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
    /// Gets or sets the user-safe issue text in stable English.
    /// </summary>
    /// <remarks>
    /// This text is never localized: it is sent back to the model as a repair instruction and it is
    /// persisted with the build record, so both a translation and a later language switch would break
    /// it. Use <see cref="VisualBriefingFailureExtensions.ToUserMessage(VisualBriefingFailure)"/> to
    /// obtain the text shown to the user.
    /// </remarks>
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