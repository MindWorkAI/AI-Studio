namespace AIStudio.Provider.Reasoning;

/// <summary>
/// What the additional API parameters of a provider say about reasoning.
/// </summary>
/// <remarks>
/// This answers a different question than ReasoningSupport does. That one says what a model is able
/// to do, and it comes from the rules. This one says what the person asked their provider for, in
/// the free-text parameters they wrote themselves -- and most of the time it says nothing at all,
/// which is a statement of its own rather than a missing answer.
/// </remarks>
public enum ReasoningConfigurationState
{
    /// <summary>
    /// No recognized reasoning parameter was found.
    /// </summary>
    NOT_CONFIGURED,

    /// <summary>
    /// A recognized reasoning parameter explicitly enables reasoning.
    /// </summary>
    EXPLICITLY_ENABLED,

    /// <summary>
    /// A recognized reasoning parameter explicitly disables reasoning.
    /// </summary>
    EXPLICITLY_DISABLED,
}