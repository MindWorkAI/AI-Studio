namespace AIStudio.Models.Matching;

/// <summary>
/// One statement about a set of model names: which names, and what holds for them.
/// </summary>
/// <param name="pattern">Which names this rule answers for.</param>
/// <param name="kind">Whether the rule chooses the model or adjusts the choice.</param>
/// <param name="change">What the rule states.</param>
/// <param name="origin">Who wrote the rule, so that a conflict can name both sides.</param>
public sealed class ModelRule(MatchPattern pattern, ModelRuleKind kind, ModelProfileChange change, string origin)
{
    /// <summary>
    /// Which names this rule answers for.
    /// </summary>
    public MatchPattern Pattern { get; } = pattern;

    /// <summary>
    /// Whether the rule chooses the model or adjusts the choice.
    /// </summary>
    public ModelRuleKind Kind { get; } = kind;

    /// <summary>
    /// What the rule states.
    /// </summary>
    public ModelProfileChange Change { get; } = change;

    /// <summary>
    /// Who wrote the rule: a family, a host, or a plugin.
    /// </summary>
    public string Origin { get; } = origin;

    /// <summary>
    /// How much this rule claims to know, worked out once when the rule is built.
    /// </summary>
    public RuleSpecificity Specificity { get; } = RuleSpecificity.Of(pattern);

    /// <summary>
    /// Names the rule in one line, for conflict reports and for breaking ties the same way twice.
    /// </summary>
    public string Description { get; } = $"{origin}: {kind} {pattern.Kind} \"{pattern.Text}\"";

    public override string ToString() => this.Description;
}