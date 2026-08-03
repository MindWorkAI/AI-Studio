namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains the result of a structured LLM stage including its single repair attempt.
/// </summary>
/// <typeparam name="T">The strict response model.</typeparam>
/// <param name="Success">Whether a validated response was produced.</param>
/// <param name="Response">The validated response.</param>
/// <param name="Issue">The final safe issue.</param>
/// <param name="FailureCode">The final stable failure code.</param>
/// <param name="ValidationRule">The stable semantic validation rule.</param>
/// <param name="Diagnostic">The final safe structured-response diagnostic.</param>
/// <param name="Attempts">The number of provider calls.</param>
/// <param name="ResponseLength">The final response character count.</param>
internal sealed record StructuredLlmStageResult<T>(
    bool Success,
    T? Response,
    string Issue,
    VisualBriefingFailureCode FailureCode,
    VisualBriefingValidationRule ValidationRule,
    VisualBriefingStructuredResponseDiagnostic? Diagnostic,
    int Attempts,
    int ResponseLength)
    where T : class;