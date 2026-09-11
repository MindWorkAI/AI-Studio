using AIStudio.Provider;

namespace AIStudio.Models;

/// <summary>
/// What a rule states about a model, as a change to what is known so far.
/// </summary>
/// <remarks>
/// A selector applies its change to nothing and so states a whole profile; a modifier applies its
/// change to whatever the selector decided. One type for both, because "adds web search" and "takes
/// web search away again" are the same kind of sentence.
///
/// Everything left unsaid stays as it was. That is what lets a rule for a variant say only what
/// makes the variant different, instead of repeating the family it belongs to.
/// </remarks>
public sealed record ModelProfileChange
{
    /// <summary>
    /// A change which states nothing.
    /// </summary>
    public static readonly ModelProfileChange NOTHING = new();

    /// <summary>
    /// Capabilities the model has.
    /// </summary>
    public Capability Adds { get; init; }

    /// <summary>
    /// Capabilities the model does not have, applied after the ones it has.
    /// </summary>
    public Capability Removes { get; init; }

    /// <summary>
    /// How the model reasons, or null to leave that as it was.
    /// </summary>
    public ReasoningSupport? Reasoning { get; init; }

    /// <summary>
    /// What the model is made for, or null to leave that as it was.
    /// </summary>
    public ModelKind? Kind { get; init; }

    /// <summary>
    /// The context window, or null to leave it as it was.
    /// </summary>
    public ContextWindow? Context { get; init; }

    /// <summary>
    /// The tokenizer reference, or null to leave it as it was.
    /// </summary>
    public TokenizerRef? Tokenizer { get; init; }

    /// <summary>
    /// The image limits, or null to leave them as they were.
    /// </summary>
    public ImageLimits? Images { get; init; }

    /// <summary>
    /// Applies this change to a profile.
    /// </summary>
    /// <remarks>
    /// The three reasoning members of the capability enum are dropped here rather than trusted to
    /// stay out: they are the vocabulary a person writes an override in, and a profile which
    /// carried them could say that a model both always reasons and reasons on request. A rule which
    /// declares one has still made a mistake, which is why the tests and the verification run look
    /// for it instead of relying on this line to hide it.
    /// </remarks>
    /// <param name="profile">What is known so far.</param>
    /// <returns>What is known afterwards.</returns>
    public ModelProfile ApplyTo(in ModelProfile profile) => profile with
    {
        Capabilities = (profile.Capabilities | this.Adds) & ~this.Removes & ~ModelProfile.REASONING_VOCABULARY,
        Reasoning = this.Reasoning ?? profile.Reasoning,
        Kind = this.Kind ?? profile.Kind,
        Context = this.Context ?? profile.Context,
        Tokenizer = this.Tokenizer ?? profile.Tokenizer,
        Images = this.Images ?? profile.Images,
    };
}