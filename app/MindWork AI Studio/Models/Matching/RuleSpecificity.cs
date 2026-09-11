namespace AIStudio.Models.Matching;

/// <summary>
/// How much a rule claims to know, computed from the rule itself.
/// </summary>
/// <remarks>
/// This is the heart of the whole rebuild. In the old rules, which branch won was decided by where
/// it stood in the file, so a block for one family could swallow another one -- the Llama block ate
/// the DeepSeek distills because it happened to come first -- and nothing in the language noticed.
/// Here nobody writes an order. A rule saying more about a name beats a rule saying less, and
/// "deepseek-r1" says more than "llama" without anyone deciding that it should.
///
/// Two rules of equal specificity which can match the same name are a mistake, not a coin toss.
/// The index reports them, and resolving still picks the same one every time, so a build never
/// depends on which rule was registered first.
/// </remarks>
/// <param name="ExplicitRank">What a rule wrote down by hand to override all of the below.</param>
/// <param name="Kind">How tightly the pattern is bound to the name.</param>
/// <param name="PatternLength">How much of the name the pattern spells out.</param>
/// <param name="Conditions">How many further name parts the rule requires or forbids.</param>
/// <param name="Binding">Whether the rule is tied to a provider, a vendor, or both.</param>
public readonly record struct RuleSpecificity(int ExplicitRank, int Kind, int PatternLength, int Conditions, int Binding) : IComparable<RuleSpecificity>
{
    /// <summary>
    /// Works out how specific a pattern is.
    /// </summary>
    /// <param name="pattern">The pattern to measure.</param>
    /// <returns>Its specificity.</returns>
    public static RuleSpecificity Of(MatchPattern pattern) => new(
        ExplicitRank: pattern.ExplicitRank,
        Kind: WeightOf(pattern.Kind),
        PatternLength: pattern.Text.Length,
        Conditions: pattern.AlsoContains.Count + pattern.NotContains.Count,
        Binding: (pattern.OnlyOn is null ? 0 : 1) + (pattern.OnlyFrom is null ? 0 : 1));

    /// <summary>
    /// Compares two specificities, most specific last.
    /// </summary>
    /// <remarks>
    /// The criteria are weighed in the order they are written in this type, and a tuple compares
    /// exactly that way: the first difference decides, the rest is never looked at. The hand
    /// written rank comes first because an emergency exit which the length of some other pattern
    /// can overrule is not an exit at all.
    /// </remarks>
    /// <param name="other">The specificity to compare against.</param>
    /// <returns>A negative number when this one is less specific, zero when they are equal.</returns>
    public int CompareTo(RuleSpecificity other) =>
        (this.ExplicitRank, this.Kind, this.PatternLength, this.Conditions, this.Binding)
        .CompareTo((other.ExplicitRank, other.Kind, other.PatternLength, other.Conditions, other.Binding));

    private static int WeightOf(MatchKind kind) => kind switch
    {
        MatchKind.EXACT => 3,
        MatchKind.PREFIX => 2,
        MatchKind.SEGMENT => 1,
        MatchKind.SUBSTRING => 0,

        _ => 0,
    };
}