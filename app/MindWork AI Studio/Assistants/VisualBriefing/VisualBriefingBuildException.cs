namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Represents an expected visual briefing pipeline failure with safe diagnostics.
/// </summary>
internal sealed class VisualBriefingBuildException : Exception
{
    /// <summary>
    /// Initializes an expected pipeline exception.
    /// </summary>
    /// <param name="code">The stable failure code.</param>
    /// <param name="stage">The failing stage.</param>
    /// <param name="userMessage">The user-safe message.</param>
    /// <param name="technicalDetails">Safe technical details.</param>
    internal VisualBriefingBuildException(VisualBriefingFailureCode code, VisualBriefingBuildStage stage, string userMessage, string technicalDetails) : base(userMessage)
    {
        this.Code = code;
        this.Stage = stage;
        this.TechnicalDetails = technicalDetails;
    }

    /// <summary>
    /// Gets the stable failure code.
    /// </summary>
    internal VisualBriefingFailureCode Code { get; }

    /// <summary>
    /// Gets the failing stage.
    /// </summary>
    internal VisualBriefingBuildStage Stage { get; }

    /// <summary>
    /// Gets technical details that exclude user content.
    /// </summary>
    internal string TechnicalDetails { get; }
}