namespace AIStudio.Provider.Reasoning;

/// <summary>
/// Reading the values a person wrote into their additional API parameters.
/// </summary>
/// <remarks>
/// Every dialect ends up asking the same two questions: is this key there, and does this value mean
/// yes or no. The answers are the same whoever asks them -- "off" is off at every provider -- so
/// they live here rather than once per dialect.
/// </remarks>
public static class ReasoningParameters
{
    /// <summary>
    /// Try to read a parameter, matching the key regardless of how it was capitalized.
    /// </summary>
    /// <param name="parameters">The parsed parameter dictionary.</param>
    /// <param name="key">The parameter name to find.</param>
    /// <param name="value">The matched parameter value, if found.</param>
    /// <returns>True, when a matching key was found.</returns>
    public static bool TryGet(IDictionary<string, object> parameters, string key, out object? value)
    {
        value = null;
        if (parameters.Count is 0)
            return false;

        var foundKey = parameters.Keys.FirstOrDefault(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
        if (foundKey is null)
            return false;

        value = parameters[foundKey];
        return true;
    }

    /// <summary>
    /// Reads a value which is written as a boolean, a number, or a level.
    /// </summary>
    /// <param name="value">The raw parsed parameter value.</param>
    /// <returns>What the value says.</returns>
    public static ReasoningConfigurationState LevelOf(object? value) => value switch
    {
        bool booleanValue => booleanValue ? ReasoningConfigurationState.EXPLICITLY_ENABLED : ReasoningConfigurationState.EXPLICITLY_DISABLED,
        int i => i is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        long l => l is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        double d => Math.Abs(d) < double.Epsilon ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        decimal m => m is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        string text when IsDisabledText(text) => ReasoningConfigurationState.EXPLICITLY_DISABLED,
        string text when IsEnabledText(text) => ReasoningConfigurationState.EXPLICITLY_ENABLED,

        _ => ReasoningConfigurationState.NOT_CONFIGURED,
    };

    /// <summary>
    /// Reads a token budget, which several providers use to say the same thing with a number.
    /// </summary>
    /// <remarks>
    /// A budget of zero switches thinking off. Everything else, negative budgets included, leaves it
    /// available -- a negative one usually means "as much as it takes".
    /// </remarks>
    /// <param name="value">The configured budget value.</param>
    /// <returns>What the budget says.</returns>
    public static ReasoningConfigurationState BudgetOf(object? value) => value switch
    {
        int i => i is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        long l => l is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        double d => Math.Abs(d) < double.Epsilon ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,
        decimal m => m is 0 ? ReasoningConfigurationState.EXPLICITLY_DISABLED : ReasoningConfigurationState.EXPLICITLY_ENABLED,

        _ => LevelOf(value),
    };

    /// <summary>
    /// Puts several answers together into one.
    /// </summary>
    /// <remarks>
    /// A "no" wins over a "yes", wherever the two stand. Somebody who switched thinking off in one
    /// place meant to switch it off, and an indicator lighting up anyway because another parameter
    /// could be read as a yes would be the app arguing with them.
    /// </remarks>
    /// <param name="states">What the dialects found.</param>
    /// <returns>The one answer.</returns>
    public static ReasoningConfigurationState Merge(IEnumerable<ReasoningConfigurationState> states)
    {
        var result = ReasoningConfigurationState.NOT_CONFIGURED;
        foreach (var state in states)
        {
            if (state is ReasoningConfigurationState.EXPLICITLY_DISABLED)
                return ReasoningConfigurationState.EXPLICITLY_DISABLED;

            if (state is ReasoningConfigurationState.EXPLICITLY_ENABLED)
                result = ReasoningConfigurationState.EXPLICITLY_ENABLED;
        }

        return result;
    }

    /// <summary>
    /// Puts several answers together into one.
    /// </summary>
    /// <param name="states">What the dialects found.</param>
    /// <returns>The one answer.</returns>
    public static ReasoningConfigurationState Merge(params ReasoningConfigurationState[] states) => Merge(states.AsEnumerable());

    /// <summary>
    /// Whether a text means yes.
    /// </summary>
    /// <param name="text">The string value to inspect.</param>
    /// <returns>True, when the value switches reasoning on.</returns>
    public static bool IsEnabledText(string text) =>
        text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("on", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("low", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("minimal", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("medium", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("high", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("max", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a text means no.
    /// </summary>
    /// <param name="text">The string value to inspect.</param>
    /// <returns>True, when the value switches reasoning off.</returns>
    public static bool IsDisabledText(string text) =>
        string.IsNullOrWhiteSpace(text) ||
        text.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("no", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("none", StringComparison.OrdinalIgnoreCase) ||
        text.Equals("disabled", StringComparison.OrdinalIgnoreCase);
}