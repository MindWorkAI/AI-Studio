namespace AIStudio.Provider;

/// <summary>
/// Describes the default reasoning behavior reported by a provider's model catalog.
/// </summary>
/// <remarks>
/// A catalog which reports this is more reliable than our model-name heuristics, so
/// <see cref="Model.ReasoningBehavior"/> takes precedence over them. Not every provider
/// reports it, which is what <see cref="UNKNOWN"/> stands for.
/// </remarks>
public enum ModelReasoningBehavior
{
    /// <summary>
    /// The catalog said nothing about reasoning. The model-name heuristics decide.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// The model can reason, but does not unless the request asks for it.
    /// </summary>
    OPTIONAL,

    /// <summary>
    /// The model reasons unless the request switches it off.
    /// </summary>
    DEFAULT_ON,

    /// <summary>
    /// The model always reasons and it cannot be switched off.
    /// </summary>
    ALWAYS_ON,
}
