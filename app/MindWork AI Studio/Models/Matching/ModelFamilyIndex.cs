using System.Collections.Frozen;

using AIStudio.Provider;

namespace AIStudio.Models.Matching;

/// <summary>
/// Answers what is known about a model name, out of all the rules there are.
/// </summary>
/// <remarks>
/// The old rules asked every question in turn: a name arriving at the open weights block walked
/// past more than a hundred string comparisons before anything answered it, and it did so on every
/// render of every component which shows a provider. Here the name is cut into its parts and each
/// part looks up the handful of rules which mention it, so a name is measured against the rules
/// which could possibly apply to it and against nothing else.
///
/// Building the index costs a sort and a dictionary; that happens once. Answering allocates a small
/// list when several rules apply, which is the cold path -- the registry keeps the answers, so the
/// same model is not resolved twice.
///
/// Nothing here reaches for application state. A test can build an index and ask it questions
/// without the app ever having started.
/// </remarks>
public sealed class ModelFamilyIndex
{
    private readonly FrozenDictionary<string, ModelRule[]>.AlternateLookup<ReadOnlySpan<char>> byNamePartLookup;
    private readonly bool canLookUpNameParts;
    private readonly ModelRule[] alwaysChecked;

    private ModelFamilyIndex(ModelRule[] rules, FrozenDictionary<string, ModelRule[]> byNamePart, ModelRule[] alwaysChecked, IReadOnlyList<RuleAmbiguity> ambiguities)
    {
        this.alwaysChecked = alwaysChecked;
        this.Rules = rules;
        this.Ambiguities = ambiguities;

        //
        // Looking a name part up as a span rather than as a string is what keeps the lookup free of
        // allocations. It needs a comparer which knows how to hash a span, and an index holding no
        // rules at all has no comparer to speak of -- there is nothing to look up in that case
        // either, so the flag simply skips the walk.
        //
        this.canLookUpNameParts = byNamePart.TryGetAlternateLookup(out this.byNamePartLookup);
    }

    /// <summary>
    /// Every rule the index was built from, ordered by name.
    /// </summary>
    public IReadOnlyList<ModelRule> Rules { get; }

    /// <summary>
    /// Rules which claim exactly the same names as another rule.
    /// </summary>
    /// <remarks>
    /// Found by comparing what the patterns say, which catches the case of two families claiming
    /// one name outright. Two patterns which merely happen to overlap on some name cannot be found
    /// this way -- deciding that in general is not a question about text any more. Those show up
    /// when a name is actually resolved, as tied selectors, which is why the verification run
    /// resolves the whole corpus instead of only reading the rules.
    /// </remarks>
    public IReadOnlyList<RuleAmbiguity> Ambiguities { get; }

    /// <summary>
    /// Builds an index over a set of rules.
    /// </summary>
    /// <param name="rules">The rules, in any order. The order they arrive in changes nothing.</param>
    /// <returns>The index.</returns>
    public static ModelFamilyIndex Build(IEnumerable<ModelRule> rules)
    {
        //
        // Sorting by name, not by specificity: the comparison does the deciding, and a stable order
        // is what makes two builds of the same rules produce the same answers, down to which rule
        // is reported first in a conflict.
        //
        var ordered = rules.OrderBy(rule => rule.Description, StringComparer.Ordinal).ToArray();
        var buckets = new Dictionary<string, List<ModelRule>>(StringComparer.Ordinal);
        var alwaysChecked = new List<ModelRule>();

        foreach (var rule in ordered)
        {
            var namePart = rule.Pattern.IndexKey();
            if (namePart.IsEmpty)
            {
                alwaysChecked.Add(rule);
                continue;
            }

            var key = namePart.ToString();
            if (!buckets.TryGetValue(key, out var bucket))
                buckets[key] = bucket = [];

            bucket.Add(rule);
        }

        var byNamePart = buckets.ToFrozenDictionary(bucket => bucket.Key, bucket => bucket.Value.ToArray(), StringComparer.Ordinal);
        return new(ordered, byNamePart, alwaysChecked.ToArray(), FindAmbiguities(ordered));
    }

    /// <summary>
    /// Says what is known about a model.
    /// </summary>
    /// <param name="id">The model name.</param>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="vendor">Who built it, as far as anybody knows.</param>
    /// <returns>The profile, which is empty when no rule knows the name.</returns>
    public ModelProfile Resolve(in ModelId id, LLMProviders provider, ModelVendor vendor) => this.Explain(id, provider, vendor).Profile;

    /// <summary>
    /// Says what is known about a model, and which rules said it.
    /// </summary>
    /// <param name="id">The model name.</param>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="vendor">Who built it, as far as anybody knows.</param>
    /// <returns>The profile together with the rules behind it.</returns>
    public ModelResolution Explain(in ModelId id, LLMProviders provider, ModelVendor vendor)
    {
        if (id.IsEmpty)
            return ModelResolution.NOTHING;

        var match = new Match();
        Consider(this.alwaysChecked, id, provider, vendor, ref match);

        if (this.canLookUpNameParts)
            foreach (var namePart in id.Segments)
                if (this.byNamePartLookup.TryGetValue(namePart, out var candidates))
                    Consider(candidates, id, provider, vendor, ref match);

        //
        // Least specific first, so that the rule saying the most about this name has the last word.
        // Sorting a list is not stable, so equally specific modifiers are ordered by name: applying
        // them in a different order could otherwise produce a different profile on another machine.
        //
        match.Modifiers?.Sort(static (left, right) =>
        {
            var order = left.Specificity.CompareTo(right.Specificity);
            return order is not 0 ? order : string.CompareOrdinal(left.Description, right.Description);
        });

        var profile = match.Selector?.Change.ApplyTo(ModelProfile.UNKNOWN) ?? ModelProfile.UNKNOWN;
        if (match.Modifiers is not null)
            foreach (var modifier in match.Modifiers)
                profile = modifier.Change.ApplyTo(profile);

        return new(profile, match.Selector, match.Modifiers ?? [], match.TiedSelectors ?? []);
    }

    private static void Consider(ModelRule[] candidates, in ModelId id, LLMProviders provider, ModelVendor vendor, ref Match match)
    {
        foreach (var rule in candidates)
        {
            if (!rule.Pattern.Matches(id, provider, vendor))
                continue;

            if (rule.Kind is ModelRuleKind.MODIFIER)
            {
                //
                // A rule can be reached twice when a name repeats one of its parts. Applying a
                // modifier twice would change nothing, but reporting it twice would read as if two
                // rules had spoken.
                //
                match.Modifiers ??= [];
                if (!match.Modifiers.Contains(rule))
                    match.Modifiers.Add(rule);

                continue;
            }

            if (match.Selector is null)
            {
                match.Selector = rule;
                continue;
            }

            if (ReferenceEquals(match.Selector, rule))
                continue;

            var order = rule.Specificity.CompareTo(match.Selector.Specificity);
            if (order > 0)
            {
                match.Selector = rule;
                match.TiedSelectors = null;
                continue;
            }

            if (order < 0)
                continue;

            //
            // Both rules claim the name with the same right, which the rules should not allow. The
            // answer still has to be the same one on every machine and in every build, so the name
            // of the rule decides rather than the order the rules arrived in.
            //
            var winner = string.CompareOrdinal(rule.Description, match.Selector.Description) < 0 ? rule : match.Selector;
            var loser = ReferenceEquals(winner, rule) ? match.Selector : rule;

            match.Selector = winner;
            (match.TiedSelectors ??= []).Add(loser);
        }
    }

    private static IReadOnlyList<RuleAmbiguity> FindAmbiguities(IReadOnlyList<ModelRule> rules)
    {
        var ambiguities = new List<RuleAmbiguity>();
        var claimed = new Dictionary<string, ModelRule>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            if (rule.Kind is not ModelRuleKind.SELECTOR)
                continue;

            var signature = rule.Pattern.Signature();
            if (claimed.TryGetValue(signature, out var other))
            {
                ambiguities.Add(new(other, rule, "Two selectors claim exactly the same model names."));
                continue;
            }

            claimed[signature] = rule;
        }

        return ambiguities;
    }

    /// <summary>
    /// What the walk over the candidate rules has found so far.
    /// </summary>
    private struct Match
    {
        public ModelRule? Selector;

        public List<ModelRule>? TiedSelectors;

        public List<ModelRule>? Modifiers;
    }
}