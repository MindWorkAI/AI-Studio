using AIStudio.Tools.RAG;

namespace AIStudio.AIOperations.RetrievalContextValidation;

/// <summary>
/// Represents the result of a retrieval context validation.
/// </summary>
/// <remarks>
/// Decision and Confidence mean nothing unless IsAnswered is true: a validation which failed says nothing about
/// its retrieval context, neither that it helps nor that it does not.
/// </remarks>
/// <param name="RetrievalContext">The retrieval context that was validated, as it is prepared for the AI.</param>
/// <param name="IsAnswered">Whether the model gave a usable answer.</param>
/// <param name="Decision">Whether the retrieval context is useful or not.</param>
/// <param name="Reason">The reason for the decision, or why the model gave no usable answer.</param>
/// <param name="Confidence">The probability that the decision is right, between 0 and 1.</param>
public readonly record struct RetrievalContextValidationResult(PreparedRetrievalContext RetrievalContext, bool IsAnswered, bool Decision, string Reason, float Confidence) : IConfidence
{
    /// <summary>
    /// A validation without a usable answer, e.g., because the model gave none or its JSON was invalid.
    /// </summary>
    /// <param name="retrievalContext">The retrieval context that was to be validated.</param>
    /// <param name="reason">Why there is no usable answer.</param>
    /// <returns>The result, with IsAnswered being false.</returns>
    public static RetrievalContextValidationResult NotAnswered(PreparedRetrievalContext retrievalContext, string reason) => new(retrievalContext, false, false, reason, 0f);
}