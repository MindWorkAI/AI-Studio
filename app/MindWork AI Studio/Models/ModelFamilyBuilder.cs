using AIStudio.Models.Matching;

namespace AIStudio.Models;

/// <summary>
/// Collects the rules of one family as they are stated.
/// </summary>
/// <remarks>
/// The order rules are stated in changes nothing about which one wins -- that is what the computed
/// specificity is for. It matters in one place only: a variant which inherits takes what the rule
/// before it stated, so that a family can say what its models have in common once and then say
/// only what makes each variant different.
/// </remarks>
/// <param name="origin">What the rules name as their origin, which is the family's name.</param>
public sealed class ModelFamilyBuilder(string origin)
{
    private readonly List<ModelRuleBuilder> stated = [];

    /// <summary>
    /// States a rule which chooses the model.
    /// </summary>
    /// <param name="text">The name, or the part of it, this rule answers for. In normalized form.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Rule(string text) => this.Add(text, ModelRuleKind.SELECTOR);

    /// <summary>
    /// States a rule which adjusts whatever chose the model.
    /// </summary>
    /// <param name="text">The name, or the part of it, this rule answers for. In normalized form.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Modifier(string text) => this.Add(text, ModelRuleKind.MODIFIER);

    /// <summary>
    /// Turns everything stated into rules.
    /// </summary>
    /// <returns>The rules, in the order they were stated.</returns>
    internal IReadOnlyList<ModelRule> Build()
    {
        var built = new List<ModelRule>(this.stated.Count);
        var byPatternText = new Dictionary<string, ModelProfileChange>(StringComparer.Ordinal);
        var statedMoreThanOnce = new HashSet<string>(StringComparer.Ordinal);
        ModelProfileChange? previous = null;

        foreach (var statement in this.stated)
        {
            var rule = statement.Build(statement.InheritanceBasis(byPatternText, statedMoreThanOnce, previous));

            built.Add(rule);

            //
            // The same text may well be stated twice, with different conditions on top -- that is
            // how a variant of a generation is written. What cannot be done afterwards is naming
            // that text to inherit from, because it no longer names one rule.
            //
            if (!byPatternText.TryAdd(rule.Pattern.Text, rule.Change))
                statedMoreThanOnce.Add(rule.Pattern.Text);

            previous = rule.Change;
        }

        return built;
    }

    private ModelRuleBuilder Add(string text, ModelRuleKind kind)
    {
        var statement = new ModelRuleBuilder(text, kind, origin);
        this.stated.Add(statement);
        return statement;
    }
}