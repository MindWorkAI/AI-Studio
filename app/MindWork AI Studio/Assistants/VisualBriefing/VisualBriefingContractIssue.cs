namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes a safe validation rejection for a structured model response.
/// </summary>
/// <param name="Code">The stable failure code.</param>
/// <param name="Issue">The user-safe validation issue.</param>
/// <param name="Rule">The stable validation rule.</param>
/// <param name="Diagnostic">The optional structured-response diagnostic.</param>
internal sealed record VisualBriefingContractIssue(
    VisualBriefingFailureCode Code,
    string Issue,
    VisualBriefingValidationRule Rule = VisualBriefingValidationRule.NONE,
    VisualBriefingStructuredResponseDiagnostic? Diagnostic = null);