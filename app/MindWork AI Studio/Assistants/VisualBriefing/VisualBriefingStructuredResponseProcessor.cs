namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Adapts the shared structured-response processor to visual briefing failures.
/// </summary>
internal static class VisualBriefingStructuredResponseProcessor
{
    /// <summary>
    /// Parses every eligible candidate and returns the last fully valid response.
    /// </summary>
    /// <typeparam name="T">The strict response type.</typeparam>
    /// <param name="answer">The complete model answer.</param>
    /// <param name="validate">The semantic stage validator.</param>
    /// <returns>The selected response or a safe issue for the repair attempt.</returns>
    internal static VisualBriefingStructuredResponseResult<T> Process<T>(
        string answer,
        Func<T, VisualBriefingContractIssue?> validate)
        where T : class
    {
        var result = StructuredResponseProcessor.Process<T, FailureIdentity>(
            answer,
            VisualBriefingJson.Canonical,
            ClassifyStructuralFailure,
            response => AdaptIssue(validate(response)));
        if (result.Response is not null)
            return new(result.Response, null);

        var issue = result.Issue!;
        return new(
            null,
            new(
                issue.Failure.Code,
                issue.Issue,
                issue.Failure.Rule,
                issue.Diagnostic));
    }

    /// <summary>
    /// Renders a compact grammar from the same CLR types used for strict parsing.
    /// </summary>
    /// <typeparam name="T">The response contract type.</typeparam>
    /// <returns>A provider-neutral contract grammar.</returns>
    internal static string BuildContractGrammar<T>()
        where T : class =>
        StructuredResponseProcessor.BuildContractGrammar<T>();

    private static StructuredResponseIssue<FailureIdentity>? AdaptIssue(
        VisualBriefingContractIssue? issue) =>
        issue is null
            ? null
            : new(
                issue.Issue,
                new(issue.Code, issue.Rule),
                issue.Diagnostic);

    private static FailureIdentity ClassifyStructuralFailure(StructuredResponseFailureKind failureKind, StructuredResponseIssueKind issueKind) => failureKind switch
        {
            StructuredResponseFailureKind.JSON_INVALID => new(
                VisualBriefingFailureCode.RESPONSE_JSON_INVALID,
                VisualBriefingValidationRule.JSON_INVALID),
            StructuredResponseFailureKind.CONTRACT_INVALID when
                issueKind is StructuredResponseIssueKind.UNKNOWN_FIELD => new(
                    VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
                    VisualBriefingValidationRule.UNKNOWN_FIELD),
            StructuredResponseFailureKind.CONTRACT_INVALID when
                issueKind is StructuredResponseIssueKind.TYPE_MISMATCH => new(
                    VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
                    VisualBriefingValidationRule.VALUE_TYPE_INVALID),
            StructuredResponseFailureKind.CONTRACT_INVALID => new(
                VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
                VisualBriefingValidationRule.JSON_INVALID),
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null),
        };

    private readonly record struct FailureIdentity(
        VisualBriefingFailureCode Code,
        VisualBriefingValidationRule Rule);
}
