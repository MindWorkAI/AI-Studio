namespace AIStudio.Models;

/// <summary>
/// How much a model can read and write in one conversation, in tokens.
/// </summary>
/// <remarks>
/// Two numbers, because the model cards name two. There is what the model does as it ships, and
/// there is what an operator can raise it to by configuring the engine, usually through one of the
/// rope-scaling settings. A self-hosted model runs at whatever its operator chose, so the second
/// number is a ceiling, not a promise.
///
/// Nothing here says "unknown" with a zero. The default value of this type is unknown, which is the
/// right answer for a model nobody has written anything about yet, and a known window can never be
/// zero tokens wide because the factory below refuses to build one.
/// </remarks>
public readonly record struct ContextWindow
{
    /// <summary>
    /// The window of a model we have no statement about.
    /// </summary>
    public static readonly ContextWindow UNKNOWN = new();

    /// <summary>
    /// Whether anything is known about this window at all. When false, both numbers are meaningless.
    /// </summary>
    public bool IsKnown { get; private init; }

    /// <summary>
    /// What the model reads and writes without anyone configuring it.
    /// </summary>
    public int DefaultTokens { get; private init; }

    /// <summary>
    /// What an operator can raise the window to, or null when it cannot be raised or nobody knows.
    /// </summary>
    public int? RaisableToTokens { get; private init; }

    /// <summary>
    /// States a known context window.
    /// </summary>
    /// <param name="defaultTokens">What the model does as it ships. Has to be greater than zero.</param>
    /// <param name="raisableTo">What an operator can raise it to. Has to be at least the default.</param>
    /// <returns>The window.</returns>
    public static ContextWindow Of(int defaultTokens, int? raisableTo = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultTokens);
        if (raisableTo is not null)
            ArgumentOutOfRangeException.ThrowIfLessThan(raisableTo.Value, defaultTokens);

        return new()
        {
            IsKnown = true,
            DefaultTokens = defaultTokens,
            RaisableToTokens = raisableTo,
        };
    }
}