using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models;

/// <summary>
/// One rule, while it is being stated.
/// </summary>
/// <remarks>
/// Everything left unsaid stays unsaid: a rule which says nothing about the context window does not
/// claim that nobody knows it, it simply makes no statement, and whatever else does gets to keep
/// its answer. That is what lets a variant state one sentence instead of repeating its family.
/// </remarks>
/// <param name="patternText">The name, or the part of it, this rule answers for. In normalized form.</param>
/// <param name="ruleKind">Whether the rule chooses the model or adjusts the choice.</param>
/// <param name="origin">What the rule names as its origin, which is the family's name.</param>
public sealed class ModelRuleBuilder(string patternText, ModelRuleKind ruleKind, string origin)
{
    private readonly List<string> alsoContains = [];
    private readonly List<string> notContains = [];

    private MatchKind matchKind = MatchKind.SEGMENT;
    private LLMProviders? onlyOn;
    private ModelVendor? onlyFrom;
    private int explicitRank;
    private bool inheritsFromPrevious;
    private string? inheritsFromText;

    private Capability adds;
    private Capability removes;
    private ReasoningSupport? reasoning;
    private ModelKind? modelKind;
    private ContextWindow? context;
    private TokenizerRef? tokenizer;
    private ImageLimits? images;

    /// <summary>
    /// The text this rule answers for, before anything was stated about it.
    /// </summary>
    /// <remarks>
    /// Read by the family builder before it builds anything, to find the texts which name more
    /// than one rule.
    /// </remarks>
    internal string PatternText => patternText;

    /// <summary>
    /// The text is the whole model name.
    /// </summary>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder AsExact() => this.MatchingAs(MatchKind.EXACT);

    /// <summary>
    /// The name begins with the text, and a name part ends there.
    /// </summary>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder AsPrefix() => this.MatchingAs(MatchKind.PREFIX);

    /// <summary>
    /// The text appears in the name as one or more whole name parts. This is the default.
    /// </summary>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder AsSegment() => this.MatchingAs(MatchKind.SEGMENT);

    /// <summary>
    /// The text appears anywhere in the name, boundaries or not.
    /// </summary>
    /// <remarks>
    /// The last resort, for the names where a vendor glues things together. It claims the least and
    /// therefore loses against every other kind.
    /// </remarks>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder AsSubstring() => this.MatchingAs(MatchKind.SUBSTRING);

    /// <summary>
    /// Further name parts the model's name has to carry.
    /// </summary>
    /// <param name="nameParts">The name parts, each in normalized form.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder AlsoContains(params string[] nameParts)
    {
        this.alsoContains.AddRange(nameParts);
        return this;
    }

    /// <summary>
    /// Name parts whose presence rules this rule out.
    /// </summary>
    /// <param name="nameParts">The name parts, each in normalized form.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder NotContains(params string[] nameParts)
    {
        this.notContains.AddRange(nameParts);
        return this;
    }

    /// <summary>
    /// Restricts this rule to one provider.
    /// </summary>
    /// <param name="provider">The provider serving the model.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder OnlyOn(LLMProviders provider)
    {
        this.onlyOn = provider;
        return this;
    }

    /// <summary>
    /// Restricts this rule to models of one vendor.
    /// </summary>
    /// <param name="vendor">Who built the model.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder OnlyFrom(ModelVendor vendor)
    {
        this.onlyFrom = vendor;
        return this;
    }

    /// <summary>
    /// What the model can do.
    /// </summary>
    /// <param name="capabilities">The capabilities, combined with the or operator.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Capabilities(Capability capabilities)
    {
        this.adds |= capabilities;
        return this;
    }

    /// <summary>
    /// Which APIs the model answers through.
    /// </summary>
    /// <remarks>
    /// The same thing as stating a capability, said separately because it reads as a different kind
    /// of sentence: what a model is able to do, and how one talks to it.
    /// </remarks>
    /// <param name="apis">The API capabilities, combined with the or operator.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Apis(Capability apis)
    {
        this.adds |= apis;
        return this;
    }

    /// <summary>
    /// What the model cannot do, applied after everything it can.
    /// </summary>
    /// <param name="capabilities">The capabilities, combined with the or operator.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Removes(Capability capabilities)
    {
        this.removes |= capabilities;
        return this;
    }

    /// <summary>
    /// How the model reasons.
    /// </summary>
    /// <param name="support">The way it reasons.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Reasoning(ReasoningSupport support)
    {
        this.reasoning = support;
        return this;
    }

    /// <summary>
    /// What the model is made for, when it is not a chat model.
    /// </summary>
    /// <param name="kind">The kind of model.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Kind(ModelKind kind)
    {
        this.modelKind = kind;
        return this;
    }

    /// <summary>
    /// How much the model reads and writes in one conversation.
    /// </summary>
    /// <param name="defaultTokens">What it does as it ships.</param>
    /// <param name="raisableTo">What an operator can raise it to, where that is documented.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder ContextWindow(int defaultTokens, int? raisableTo = null)
    {
        this.context = Models.ContextWindow.Of(defaultTokens, raisableTo);
        return this;
    }

    /// <summary>
    /// Which tokenizer counts this model's tokens.
    /// </summary>
    /// <param name="kind">What sort of tokenizer it is.</param>
    /// <param name="id">Its name, in whatever spelling that sort uses.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Tokenizer(TokenizerKind kind, string id)
    {
        this.tokenizer = new TokenizerRef(kind, id);
        return this;
    }

    /// <summary>
    /// How many images the model accepts.
    /// </summary>
    /// <param name="maxPerMessage">How many fit into one message, where that is documented.</param>
    /// <param name="maxPerRequest">How many fit into one request, where that is documented.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Images(int? maxPerMessage = null, int? maxPerRequest = null)
    {
        this.images = new ImageLimits(maxPerMessage, maxPerRequest);
        return this;
    }

    /// <summary>
    /// Takes everything the rule stated before this one and goes on from there.
    /// </summary>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Inherits()
    {
        this.inheritsFromPrevious = true;
        return this;
    }

    /// <summary>
    /// Takes everything one particular rule of this family stated and goes on from there.
    /// </summary>
    /// <remarks>
    /// Worth preferring over the plain form in a family with more than one generation: naming the
    /// rule survives somebody reordering the file, while "the one before" does not.
    /// </remarks>
    /// <param name="inheritedPatternText">The text of the rule to inherit from.</param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder InheritsFrom(string inheritedPatternText)
    {
        this.inheritsFromText = inheritedPatternText;
        return this;
    }

    /// <summary>
    /// Moves this rule ahead of, or behind, everything the computed specificity would decide.
    /// </summary>
    /// <remarks>
    /// The emergency exit, and it is meant to stay unused.
    /// </remarks>
    /// <param name="rank">Positive to move the rule ahead, negative to push it back.</param>
    /// <param name="reason">
    /// What the computation gets wrong here. It is not kept: it stands in the source so that the
    /// next reader finds an explanation next to the rank instead of a number nobody can account for.
    /// </param>
    /// <returns>The rule, to go on stating.</returns>
    public ModelRuleBuilder Rank(int rank, string reason)
    {
        _ = reason;
        this.explicitRank = rank;
        return this;
    }

    /// <summary>
    /// What this rule goes on from, if it goes on from anything.
    /// </summary>
    /// <param name="byPatternText">What the rules stated so far, by their pattern text.</param>
    /// <param name="statedMoreThanOnce">The texts which name more than one rule of this family.</param>
    /// <param name="previous">What the rule stated right before this one, if there was one.</param>
    /// <returns>The statement to start from, or null when the rule states everything itself.</returns>
    internal ModelProfileChange? InheritanceBasis(IReadOnlyDictionary<string, ModelProfileChange> byPatternText, IReadOnlySet<string> statedMoreThanOnce, ModelProfileChange? previous)
    {
        if (this.inheritsFromText is not null)
        {
            //
            // A text stated twice names two rules, and taking whichever happened to come last
            // would be a coin toss nobody sees. The way out is the plain form, which says "the one
            // before" and means exactly one rule.
            //
            if (statedMoreThanOnce.Contains(this.inheritsFromText))
                throw new InvalidOperationException($"The rule \"{patternText}\" of {origin} inherits from \"{this.inheritsFromText}\", which this family states more than once. Use Inherits() right after the rule to go on from, or give the rule a text of its own.");

            return byPatternText.TryGetValue(this.inheritsFromText, out var named)
                ? named
                : throw new InvalidOperationException($"The rule \"{patternText}\" of {origin} inherits from \"{this.inheritsFromText}\", which this family does not state before it.");
        }

        if (!this.inheritsFromPrevious)
            return null;

        return previous ?? throw new InvalidOperationException($"The rule \"{patternText}\" of {origin} inherits, but it is the first rule this family states.");
    }

    /// <summary>
    /// Turns the statement into a rule.
    /// </summary>
    /// <param name="basis">What to go on from, or null to state everything from nothing.</param>
    /// <returns>The rule.</returns>
    internal ModelRule Build(ModelProfileChange? basis)
    {
        var pattern = new MatchPattern
        {
            Kind = this.matchKind,
            Text = patternText,
            AlsoContains = this.alsoContains.ToArray(),
            NotContains = this.notContains.ToArray(),
            OnlyOn = this.onlyOn,
            OnlyFrom = this.onlyFrom,
            ExplicitRank = this.explicitRank,
        };

        return new(pattern, ruleKind, this.ChangeOnTopOf(basis), origin);
    }

    private ModelProfileChange ChangeOnTopOf(ModelProfileChange? basis) => new()
    {
        //
        // What this rule states wins over what it inherited, in both directions: a variant may take
        // away what its family has, and it may hand back what its family took away.
        //
        Adds = ((basis?.Adds ?? Capability.NONE) | this.adds) & ~this.removes,
        Removes = ((basis?.Removes ?? Capability.NONE) | this.removes) & ~this.adds,
        Reasoning = this.reasoning ?? basis?.Reasoning,
        Kind = this.modelKind ?? basis?.Kind,
        Context = this.context ?? basis?.Context,
        Tokenizer = this.tokenizer ?? basis?.Tokenizer,
        Images = this.images ?? basis?.Images,
    };

    private ModelRuleBuilder MatchingAs(MatchKind kind)
    {
        this.matchKind = kind;
        return this;
    }
}