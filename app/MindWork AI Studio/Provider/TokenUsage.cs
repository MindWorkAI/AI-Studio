namespace AIStudio.Provider;

/// <summary>
/// What a provider said one request actually carried, in tokens.
/// </summary>
/// <remarks>
/// The counterpart to what the app counts for itself: the app estimates what the next request will
/// cost, while this is what the provider counted for the last one. Two different statements, and
/// this one is the only exact one of the two.
///
/// Only the prompt is kept. Providers state what the answer cost as well, but that number includes
/// the model's reasoning and whatever the model wrote between think tags, and neither of them ever
/// becomes part of the answer's text. No later request carries them, so the number has no place in
/// a statement about those requests.
///
/// Nothing here says "unknown" with a zero. The default value of this type is unknown, which is the
/// right answer for every provider which reports nothing, and a counted request can never cost zero
/// prompt tokens because the factory below refuses to build one.
/// </remarks>
public readonly record struct TokenUsage
{
    /// <summary>
    /// The usage of a request nobody reported anything about.
    /// </summary>
    public static readonly TokenUsage UNKNOWN = new();

    /// <summary>
    /// Whether a provider reported anything at all. When false, the number is meaningless.
    /// </summary>
    public bool IsKnown { get; private init; }

    /// <summary>
    /// What everything sent to the model cost: the conversation so far, its attachments, the system
    /// prompt, and whatever tools were offered.
    /// </summary>
    public int PromptTokens { get; private init; }

    /// <summary>
    /// States what a provider reported.
    /// </summary>
    /// <remarks>
    /// A prompt of zero is not a report, because there is no request without one, and a provider
    /// sending it means we read the wrong field.
    /// </remarks>
    /// <param name="promptTokens">What the request carried. Has to be greater than zero.</param>
    /// <returns>The usage.</returns>
    public static TokenUsage Of(int promptTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(promptTokens);

        return new()
        {
            IsKnown = true,
            PromptTokens = promptTokens,
        };
    }

    /// <summary>
    /// States what a provider reported, or unknown when it reported nothing usable.
    /// </summary>
    /// <remarks>
    /// For the reading side, where the number comes out of somebody else's JSON: a missing field, a
    /// null, or a zero all mean the same thing there, and none of them is worth an exception.
    /// </remarks>
    /// <param name="promptTokens">What the request carried, as the provider stated it.</param>
    /// <returns>The usage, or UNKNOWN.</returns>
    public static TokenUsage OfReported(int? promptTokens) => promptTokens is > 0 ? Of(promptTokens.Value) : UNKNOWN;
}