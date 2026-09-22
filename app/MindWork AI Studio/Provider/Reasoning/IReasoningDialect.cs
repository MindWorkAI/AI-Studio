namespace AIStudio.Provider.Reasoning;

/// <summary>
/// One way of asking a request to think, and how to recognize it.
/// </summary>
/// <remarks>
/// A dialect reads parameters and says nothing else. It does not know which provider it is being
/// asked for, it keeps no state, and it never looks at the model -- what a model is able to do comes
/// from the rules, and mixing the two is what made the code this replaces hard to follow.
/// </remarks>
public interface IReasoningDialect
{
    /// <summary>
    /// Which dialect this is, which is also where it stands in the order.
    /// </summary>
    ReasoningDialect Dialect { get; }

    /// <summary>
    /// Reads what these parameters say about reasoning.
    /// </summary>
    /// <param name="parameters">The parsed additional API parameters.</param>
    /// <returns>What they say, which is usually nothing.</returns>
    ReasoningConfigurationState Detect(IDictionary<string, object> parameters);
}