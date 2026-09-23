// ReSharper disable ClassNeverInstantiated.Global
namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What an OpenAI-compatible provider reports a chat completion cost.
/// </summary>
/// <remarks>
/// Every number is optional because this is somebody else's JSON: the block arrives only when the
/// request asked for it, and the providers which follow the shape loosely leave fields out. Reading
/// it is one thing, believing it another -- TokenUsage.OfReported decides that.
/// </remarks>
public sealed record ChatCompletionUsage
{
    /// <summary>
    /// What everything sent to the model cost.
    /// </summary>
    public int? PromptTokens { get; init; }

    /// <summary>
    /// What the model wrote in answer.
    /// </summary>
    public int? CompletionTokens { get; init; }

    /// <summary>
    /// States what this block reports, as far as it can be believed.
    /// </summary>
    /// <remarks>
    /// The one way from the wire to a usage, shared by every stream line which carries this block,
    /// so that what counts as believable is decided in a single place.
    /// </remarks>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the block states nothing usable.</returns>
    public TokenUsage ToTokenUsage() => TokenUsage.OfReported(this.PromptTokens, this.CompletionTokens);
}