namespace AIStudio.Agents;

/// <summary>
/// Represents the result of a retrieval context validation.
/// </summary>
/// <param name="Decision">Whether the retrieval context is useful or not.</param>
/// <param name="Reason">The reason for the decision.</param>
/// <param name="Confidence">The confidence of the decision.</param>
public readonly record struct RetrievalContextValidationResult(bool Decision, string Reason, float Confidence);