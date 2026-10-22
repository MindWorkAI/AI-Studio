using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenWeights;

/// <summary>
/// The models we know cannot call functions.
/// </summary>
/// <remarks>
/// Grouped by the one thing they have in common rather than by who built them, because that one
/// thing is the only reason they need a rule at all: a model nobody wrote a rule for is assumed to
/// call functions, and for these that assumption is wrong. None of them documents a tool template
/// -- the publicly funded European models, the discontinued Occiglot, the Yi line whose open
/// weights speak plain ChatML while only the closed Yi-Large-FC calls functions, and the older
/// generations of three families whose newer ones do.
///
/// Two of them have a variant built for tool use, and those step out of the way by name: Salamandra
/// ships one, and so does Falcon-H1. Everything they need is the ordinary assumption, so the rules
/// here simply do not speak for them.
///
/// This is the file which pays for the rest of the open weights not being written down. Whoever
/// runs something we never heard of gets an answer that fits the overwhelming majority of
/// instruction-tuned models, and the handful where that guess goes the wrong way are named here.
/// </remarks>
public sealed class WithoutToolCallingFamily : ModelFamily
{
    private const Capability WHAT_A_PLAIN_CHAT_MODEL_DOES = TEXT_INPUT | TEXT_OUTPUT;

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/docs/hub/en/chat-templates", new DateOnly(2026, 9, 11), "Ported unchanged from the list of models without tool calling in ProviderExtensions.OpenSource.cs, together with the tool-less generations of its OLMo, SmolLM, and Falcon blocks.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // The publicly funded European models:
        builder.Rule("teuken").AsSubstring()
            .Capabilities(WHAT_A_PLAIN_CHAT_MODEL_DOES)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("eurollm").AsSubstring().Inherits();

        builder.Rule("occiglot").AsSubstring().Inherits();

        builder.Rule("salamandra").AsSubstring().NotContains("tools").Inherits();

        //
        // The Yi line. Written as a name part rather than as a substring, so that the two letters
        // do not claim every model which happens to contain them.
        //
        builder.Rule("yi").AsSegment().Inherits();

        // The generations before OLMo 3, SmolLM 3, and Falcon 3, which have no tool template:
        builder.Rule("olmo2").AsSubstring().Inherits();

        builder.Rule("olmo-2").AsSubstring().Inherits();

        builder.Rule("smollm2").AsSubstring().Inherits();

        builder.Rule("smollm-2").AsSubstring().Inherits();

        builder.Rule("falcon-h1").AsSubstring().NotContains("tool-calling").Inherits();
    }
}