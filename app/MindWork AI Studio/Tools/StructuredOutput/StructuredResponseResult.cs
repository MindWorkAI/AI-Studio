namespace AIStudio.Tools;

/// <summary>
/// Contains a parsed structured response or its safe rejection.
/// </summary>
/// <typeparam name="TResponse">The strict response type.</typeparam>
/// <typeparam name="TFailure">The consumer-specific failure classification.</typeparam>
/// <param name="Response">The fully validated response.</param>
/// <param name="Issue">The safe rejection.</param>
internal sealed record StructuredResponseResult<TResponse, TFailure>(
    TResponse? Response,
    StructuredResponseIssue<TFailure>? Issue)
    where TResponse : class;