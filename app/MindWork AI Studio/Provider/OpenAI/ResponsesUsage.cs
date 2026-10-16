// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What OpenAI reports a Responses API call cost, as it closes the stream.
/// </summary>
/// <remarks>
/// The input tokens are the whole request. What the API took from its cache is a share of them,
/// not an addition to them, which is why that detail stays unread. Read on 2026-09-24 at
/// https://developers.openai.com/api/docs/guides/prompt-caching.
///
/// The block states more than this, the output and its reasoning share among it. Those are left
/// unread on purpose, for the reason given at TokenUsage: no later request carries them.
/// </remarks>
public sealed record ResponsesUsage
{
    /// <summary>
    /// What everything sent to the model cost, the cached part included.
    /// </summary>
    public int? InputTokens { get; init; }

    /// <summary>
    /// States what this block reports, as far as it can be believed.
    /// </summary>
    /// <remarks>
    /// Whether the block describes the request at all is not decided here but by the response
    /// around it, cf. ResponsesResponse.
    /// </remarks>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the block states nothing usable.</returns>
    public TokenUsage ToTokenUsage() => TokenUsage.OfReported(this.InputTokens);
}