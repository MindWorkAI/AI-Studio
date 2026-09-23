using AIStudio.Provider;

namespace AIStudio.Chat;

/// <summary>
/// What a provider said the request behind one answer cost, as it is stored on that answer.
/// </summary>
/// <remarks>
/// The numbers and the model they were charged for travel as one value. Each of them is meaningless
/// without the others, and loose fields on the answer could be set, cleared, or copied apart.
///
/// Plain numbers rather than a TokenUsage: that type only ever comes out of its own factory, which
/// is what keeps an impossible usage from existing, while a stored value has to be readable back by
/// the serializer. ToTokenUsage is the way back, and it treats a chat file somebody edited by hand
/// the same way the reading side treats a provider's JSON.
/// </remarks>
public sealed record ReportedTokenUsage
{
    /// <summary>
    /// What everything sent to the model cost.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// What the model wrote in answer.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Which model the numbers were charged for.
    /// </summary>
    /// <remarks>
    /// A token count belongs to the tokenizer which produced it. Switch the model of a chat, and
    /// the same conversation is worth a different number of tokens -- so the reported one stops
    /// being an answer about the request which is about to be sent, and the estimate, wrong as it
    /// is, is at least wrong about the right model.
    /// </remarks>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>
    /// How many blocks the conversation had when the request went out, this answer included.
    /// </summary>
    /// <remarks>
    /// What tells an outdated report apart without anybody having to remember anything about it.
    /// Blocks are only ever added at the end, so while this answer is the last block, a thread with
    /// the same count is the thread the provider saw. A lower count means an earlier message was
    /// deleted, and that message is still inside the reported number.
    ///
    /// Taken when the request is sent rather than when the report arrives: whatever is deleted
    /// while the answer streams in was still part of what the provider counted.
    /// </remarks>
    public int BlockCount { get; init; }

    /// <summary>
    /// States the stored numbers as a usage again.
    /// </summary>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the stored numbers state nothing usable.</returns>
    public TokenUsage ToTokenUsage() => TokenUsage.OfReported(this.PromptTokens, this.CompletionTokens);
}