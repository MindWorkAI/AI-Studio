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
    /// What the provider says both of them add up to. Read but not relied upon: it is the sum of
    /// the other two wherever a provider fills all three, and this way a provider which sends only
    /// this one is not a reason to throw the other numbers away.
    /// </summary>
    public int? TotalTokens { get; init; }
}