using AIStudio.Provider;

namespace AIStudio.Models;

/// <summary>
/// Everything the app knows about one model.
/// </summary>
/// <remarks>
/// This is the answer the registry gives, and it is a struct on purpose. The question is asked from
/// inside components which re-render on every streamed chunk, so an answer which allocates a list
/// each time is an answer asked too often. Testing a capability is one bit test here, and because
/// the value cannot be changed after it was built, the same answer can be handed to every caller.
///
/// The reasoning question is answered by the Reasoning field alone. The three reasoning members of
/// the capability enum are override vocabulary and are never part of Capabilities, so that the
/// contradictory combinations of them cannot be expressed in a result at all.
/// </remarks>
public readonly record struct ModelProfile
{
    /// <summary>
    /// The three capability members which say something about reasoning.
    /// </summary>
    /// <remarks>
    /// They are the vocabulary a person writes an override in, not something a profile carries.
    /// Kept here as one value so that the rule engine, the tests, and the verification run all mean
    /// the same three members by it.
    /// </remarks>
    public const Capability REASONING_VOCABULARY = Capability.OPTIONAL_REASONING | Capability.ALWAYS_REASONING | Capability.REASONING_BY_DEFAULT;

    /// <summary>
    /// What we know about a model nobody has written a rule for.
    /// </summary>
    /// <remarks>
    /// Nothing, which is what the default value of this type says already. Note that this still
    /// reports the model as a chat model: that is the deliberate fallback of ModelKind, because a
    /// model we fail to recognize has to stay visible to the user rather than disappear.
    /// </remarks>
    public static readonly ModelProfile UNKNOWN = new();

    /// <summary>
    /// What the app assumes about a model when no rule says anything about it.
    /// </summary>
    /// <remarks>
    /// Hugging Face alone carries more than a hundred thousand models, so falling through here is
    /// the normal case rather than a gap somebody forgot to close. The assumption describes what an
    /// instruction-tuned model of the last few years does: it reads and writes text, it speaks the
    /// chat completion API, and it calls functions.
    ///
    /// Tool calling is the part that was weighed rather than observed. Counted over the corpus, 17
    /// of the models which reach this answer would be described wrongly without it and 8 with it --
    /// and those 8 are named, in WithoutToolCallingFamily. A model that is offered tools it cannot
    /// use fails visibly, and the person turns tool calling off in the expert settings; a model
    /// that is never offered any fails invisibly, because nothing ever asks it. On top of that, a
    /// model released from here on is far more likely to call functions than not.
    ///
    /// This is the whole assumption. Everything else stays unknown on purpose: a context window
    /// nobody stated is not 4096 tokens, and a model whose name says nothing about images does not
    /// get image input for free -- that is what the expert settings and the model plugins are for.
    /// </remarks>
    public static readonly ModelProfile ASSUMED = new()
    {
        Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.CHAT_COMPLETION_API | Capability.FUNCTION_CALLING,
    };

    /// <summary>
    /// What the model can do.
    /// </summary>
    public Capability Capabilities { get; init; }

    /// <summary>
    /// How the model reasons.
    /// </summary>
    public ReasoningSupport Reasoning { get; init; }

    /// <summary>
    /// What the model is made for.
    /// </summary>
    public ModelKind Kind { get; init; }

    /// <summary>
    /// How much the model can read and write in one conversation.
    /// </summary>
    public ContextWindow Context { get; init; }

    /// <summary>
    /// Which tokenizer counts this model's tokens.
    /// </summary>
    public TokenizerRef Tokenizer { get; init; }

    /// <summary>
    /// How many images the model accepts.
    /// </summary>
    public ImageLimits Images { get; init; }

    /// <summary>
    /// Whether the model has every one of the given capabilities.
    /// </summary>
    /// <remarks>
    /// Asking for no capability at all is a mistake rather than a question with a trivial answer,
    /// which is why it says no: without that, a variable which happens to hold NONE would report
    /// every model as able to do it.
    /// </remarks>
    /// <param name="capability">One capability, or several combined with the or operator.</param>
    /// <returns>True, when the model has all of them.</returns>
    public bool Has(Capability capability) => capability is not Capability.NONE && (this.Capabilities & capability) == capability;

    /// <summary>
    /// Whether the model has at least one of the given capabilities.
    /// </summary>
    /// <param name="capabilities">Several capabilities combined with the or operator.</param>
    /// <returns>True, when the model has any of them.</returns>
    public bool HasAny(Capability capabilities) => (this.Capabilities & capabilities) is not Capability.NONE;
}