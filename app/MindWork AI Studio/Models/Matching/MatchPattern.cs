using AIStudio.Provider;

namespace AIStudio.Models.Matching;

/// <summary>
/// What a rule says about the names it answers for.
/// </summary>
/// <remarks>
/// A pattern is written in the normalized form a model name is brought into: lowercase, hyphens
/// between the parts, dots kept. A pattern which is not in that form can never match anything, so
/// it is a mistake rather than a rule which happens to be quiet.
///
/// The extra conditions and the bindings are not only there to narrow a pattern down. They also
/// make it more specific, which is how a rule earns the right to win against a shorter one without
/// anybody writing an order.
/// </remarks>
public sealed record MatchPattern
{
    /// <summary>
    /// How tightly the text is bound to the name.
    /// </summary>
    public required MatchKind Kind { get; init; }

    /// <summary>
    /// The text to look for, in normalized form.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Name parts which have to be present as well.
    /// </summary>
    /// <remarks>
    /// Each one is looked for as a whole name part, the same way the SEGMENT kind looks for its
    /// text. Writing a hyphen into one of these is therefore both unnecessary and impossible: it
    /// would not be a normalized pattern any more.
    /// </remarks>
    public IReadOnlyList<string> AlsoContains { get; init; } = [];

    /// <summary>
    /// Name parts whose presence rules this pattern out.
    /// </summary>
    public IReadOnlyList<string> NotContains { get; init; } = [];

    /// <summary>
    /// The provider this rule is written for, or null when it holds anywhere.
    /// </summary>
    /// <remarks>
    /// This is what settles the cases where one name means two models depending on who serves it.
    /// On Alibaba, "qwq" is qwq-plus, a commercial model; everywhere else it is the open weights
    /// built on Qwen 2.5. Two rules, one of them bound.
    /// </remarks>
    public LLMProviders? OnlyOn { get; init; }

    /// <summary>
    /// The vendor this rule is written for, or null when it holds for any.
    /// </summary>
    /// <remarks>
    /// A gateway which unwraps "anthropic/claude-sonnet-4-0" knows who built the model, and a rule
    /// may insist on that instead of trusting a name.
    /// </remarks>
    public ModelVendor? OnlyFrom { get; init; }

    /// <summary>
    /// Moves this rule ahead of, or behind, everything the computed specificity would decide.
    /// </summary>
    /// <remarks>
    /// The emergency exit, and it is meant to stay unused: the whole point of computing specificity
    /// is that nobody writes an order by hand any more. A rule which sets this needs a comment
    /// saying what the computation gets wrong, because the next person will read the rank as noise
    /// otherwise. Negative values push a rule back.
    /// </remarks>
    public int ExplicitRank { get; init; }

    /// <summary>
    /// Whether every text of this pattern is written in normalized form.
    /// </summary>
    /// <remarks>
    /// Normalizing is idempotent, so a text is normalized exactly when normalizing does not change
    /// it. The compile time rule checks the same thing; this is what the tests and the verification
    /// run use, and what catches a pattern which arrived from a plugin rather than from source.
    /// </remarks>
    public bool IsWellFormed => IsNormalized(this.Text) && this.AlsoContains.All(IsNormalized) && this.NotContains.All(IsNormalized);

    /// <summary>
    /// Whether this pattern answers for the given model.
    /// </summary>
    /// <param name="id">The model name, already normalized.</param>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="vendor">Who built it, as far as anybody knows.</param>
    /// <returns>True, when the rule applies.</returns>
    public bool Matches(in ModelId id, LLMProviders provider, ModelVendor vendor)
    {
        if (this.OnlyOn is not null && this.OnlyOn.Value != provider)
            return false;

        if (this.OnlyFrom is not null && this.OnlyFrom.Value != vendor)
            return false;

        if (!this.MatchesText(id))
            return false;

        foreach (var required in this.AlsoContains)
            if (!id.ContainsSegments(required))
                return false;

        foreach (var forbidden in this.NotContains)
            if (id.ContainsSegments(forbidden))
                return false;

        return true;
    }

    /// <summary>
    /// The name part the index files this pattern under, or an empty span when it cannot file it.
    /// </summary>
    /// <remarks>
    /// A pattern which is bound to the start of a name, or to whole name parts, always begins at a
    /// name part, so the first part of the pattern has to appear as a part of any name it matches.
    /// That is what lets the index skip it for every other name. A substring pattern makes no such
    /// promise and has to be checked against every name.
    /// </remarks>
    /// <returns>The first name part of the pattern, or empty.</returns>
    public ReadOnlySpan<char> IndexKey()
    {
        if (this.Kind is MatchKind.SUBSTRING || string.IsNullOrWhiteSpace(this.Text))
            return [];

        var text = this.Text.AsSpan();
        var separator = text.IndexOf(ModelId.SEGMENT_SEPARATOR);
        return separator is -1 ? text : text[..separator];
    }

    /// <summary>
    /// Everything about this pattern which decides what it matches, as one line of text.
    /// </summary>
    /// <remarks>
    /// Two patterns with the same signature match exactly the same names, which is how the index
    /// finds the rules that collide without having to reason about what a pattern could match. The
    /// conditions are sorted, because stating them in a different order states the same thing.
    /// </remarks>
    /// <returns>The signature.</returns>
    public string Signature()
    {
        var required = string.Join(',', this.AlsoContains.Order(StringComparer.Ordinal));
        var forbidden = string.Join(',', this.NotContains.Order(StringComparer.Ordinal));
        return $"{this.Kind}|{this.Text}|{this.OnlyOn}|{this.OnlyFrom}|+{required}|-{forbidden}";
    }

    /// <summary>
    /// Whether a text is written the way a normalized model name is written.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True, when normalizing it would change nothing.</returns>
    public static bool IsNormalized(string text) => !string.IsNullOrEmpty(text) && string.Equals(new ModelId(text).Normalized, text, StringComparison.Ordinal);

    private bool MatchesText(in ModelId id) => this.Kind switch
    {
        MatchKind.EXACT => id.EqualsText(this.Text),
        MatchKind.PREFIX => id.StartsWithSegments(this.Text),
        MatchKind.SEGMENT => id.ContainsSegments(this.Text),
        MatchKind.SUBSTRING => id.ContainsText(this.Text),

        _ => false,
    };
}