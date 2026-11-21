namespace AIStudio.AIOperations;

/// <summary>
/// The provider chosen for an AI operation, together with the reasons why others were not chosen.
/// </summary>
/// <remarks>
/// The reasons let a caller explain the choice instead of guessing it, e.g., why the model of the
/// assistant cleaned a web page although the user picked another one for that.
/// </remarks>
/// <param name="Provider">The chosen provider; Provider.NONE when no candidate passed.</param>
/// <param name="PreferredIssue">Why the preferred provider of the operation was not chosen: NONE when it was, NOT_SET when there is none.</param>
/// <param name="FallbackIssue">Only when no provider was chosen: why the first named fallback failed, NOT_SET when the caller named none. NONE otherwise.</param>
public sealed record AIOperationProviderChoice(Settings.Provider Provider, AIOperationProviderIssue PreferredIssue, AIOperationProviderIssue FallbackIssue)
{
    /// <summary>
    /// Whether a provider was chosen.
    /// </summary>
    public bool HasProvider => this.Provider != Settings.Provider.NONE;

    /// <summary>
    /// Whether a fallback was chosen instead of the preferred provider.
    /// </summary>
    public bool UsesFallback => this.HasProvider && this.PreferredIssue is not AIOperationProviderIssue.NONE;

    /// <summary>
    /// Chooses the provider for an AI operation from the facts about its candidates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The preferred provider wins when somebody named it, when it exists, when the organization did not
    /// prohibit it, when it reaches the global minimum, the minimum of the operation, and the confidence the
    /// content requires, and when it passes the check of the caller.
    /// </para>
    /// <para>
    /// Otherwise, the first fallback in the order of the caller wins which passes the same. The minimum of
    /// the operation applies to the fallbacks only when the rules say so; a prohibition, the global minimum,
    /// the confidence the content requires, and the check of the caller apply to every candidate.
    /// </para>
    /// </remarks>
    /// <param name="preferred">The provider picked for the operation itself.</param>
    /// <param name="fallbacks">The providers to fall back on, in the order of the caller.</param>
    /// <param name="rules">The confidence levels to reach.</param>
    /// <returns>The chosen provider with the reasons why the others were not chosen.</returns>
    public static AIOperationProviderChoice Choose(AIOperationProviderCandidate preferred, IReadOnlyList<AIOperationProviderCandidate> fallbacks, AIOperationProviderRules rules)
    {
        var preferredIssue = Check(preferred, rules, false);
        if (preferredIssue is AIOperationProviderIssue.NONE)
            return new(preferred.Provider, AIOperationProviderIssue.NONE, AIOperationProviderIssue.NONE);

        // A named candidate never reports NOT_SET, so this stays NOT_SET until the first named fallback fails:
        var fallbackIssue = AIOperationProviderIssue.NOT_SET;
        foreach (var fallback in fallbacks)
        {
            var issue = Check(fallback, rules, true);
            if (issue is AIOperationProviderIssue.NONE)
                return new(fallback.Provider, preferredIssue, AIOperationProviderIssue.NONE);

            if (fallbackIssue is AIOperationProviderIssue.NOT_SET)
                fallbackIssue = issue;
        }

        return new(Settings.Provider.NONE, preferredIssue, fallbackIssue);
    }

    /// <summary>
    /// Finds the first rule a candidate breaks, in the order of AIOperationProviderIssue.
    /// </summary>
    private static AIOperationProviderIssue Check(AIOperationProviderCandidate candidate, AIOperationProviderRules rules, bool isFallback)
    {
        if (!candidate.IsNamed)
            return AIOperationProviderIssue.NOT_SET;

        if (!candidate.Exists)
            return AIOperationProviderIssue.MISSING;

        if (candidate.IsProhibited)
            return AIOperationProviderIssue.PROHIBITED;

        if (candidate.Confidence < rules.GlobalMinimumConfidence)
            return AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE;

        if ((!isFallback || rules.OwnMinimumAppliesToFallbacks) && candidate.Confidence < rules.OwnMinimumConfidence)
            return AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE;

        if (candidate.Confidence < rules.RequiredConfidence)
            return AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT;

        return candidate.CallerIssue;
    }
}
