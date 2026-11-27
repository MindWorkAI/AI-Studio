namespace AIStudio.AIOperations.RetrievalContextValidation;

/// <summary>
/// What a model writes when it judges whether a retrieval context helps with a prompt.
/// </summary>
/// <remarks>
/// Every value may be missing from the answer, and a missing value stays null instead of turning into the
/// default of its type. An answer without a decision or a confidence is no usable answer, see
/// RetrievalContextValidationOperation.ReadAnswer.
/// </remarks>
/// <param name="Decision">Whether the retrieval context helps with the prompt.</param>
/// <param name="Reason">Why the model decided so.</param>
/// <param name="Confidence">The probability that the decision is right, between 0 and 1.</param>
public readonly record struct RetrievalContextValidationAnswer(bool? Decision, string? Reason, float? Confidence);