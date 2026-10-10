namespace AIStudio.Tools;

/// <summary>
/// Describes a safe structured-response rejection and lets its consumer attach a typed failure.
/// </summary>
/// <typeparam name="TFailure">The consumer-specific failure classification.</typeparam>
/// <param name="Issue">The user-safe issue.</param>
/// <param name="Failure">The consumer-specific failure classification.</param>
/// <param name="Diagnostic">The optional content-free diagnostic.</param>
internal sealed record StructuredResponseIssue<TFailure>(
    string Issue,
    TFailure Failure,
    StructuredResponseDiagnostic? Diagnostic = null);