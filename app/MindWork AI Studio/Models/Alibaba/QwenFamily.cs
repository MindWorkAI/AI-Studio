using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// Qwen as everybody except Alibaba Cloud serves it: the open weights.
/// </summary>
/// <remarks>
/// The counterpart to the Model Studio families next door, and the reason those are bound to their
/// provider. Alibaba sells commercial models under the same family names, and for several of them
/// it promises something else than the published checkpoint does. Nothing here is bound: these
/// rules answer wherever the weights are run, which is every gateway and every engine somebody
/// starts on their own machine.
///
/// The whole line calls functions, from Qwen 2.5 on, and the Coder checkpoints are built for
/// exactly that. Thinking is not promised by the fallback: the older generations cannot do it, and
/// which of the newer ones think by default differs per checkpoint, so those say it one by one.
/// </remarks>
public sealed class QwenFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/Qwen", new DateOnly(2026, 9, 11), "Ported unchanged from the Qwen block of ProviderExtensions.OpenSource.cs, which is the one answering everywhere but Alibaba Cloud.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        //
        // A substring, because the version grows straight out of the family name: there is no name
        // part "qwen" in "qwen2.5-72b-instruct". It is also the weakest thing a rule can be, which
        // is what lets every rule below beat it without anybody arranging an order.
        //
        builder.Rule("qwen").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // The VL checkpoints are the ones built to look at pictures:
        builder.Rule("qwen").AsSubstring().AlsoContains("vl").Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT);

        // Qwen 3.5 sees, and thinks when the request asks it to:
        builder.Rule("qwen3.5").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("qwen3.6").AsPrefix().Inherits()
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        //
        // The 3.8 tier without a size is the 27B checkpoint: that is what a rolling tag such as
        // "qwen3.8:latest" resolves to, so it is what the tier may promise.
        //
        builder.Rule("qwen3.8").AsPrefix().InheritsFrom("qwen3.6");

        // Flash-Next is the published checkpoint and Flash the production model; both watch videos:
        builder.Rule("qwen3.8-flash").AsPrefix().InheritsFrom("qwen3.8")
            .Capabilities(VIDEO_INPUT);

        //
        // Blablador writes the 27B checkpoint in two further ways, and no normalization turns
        // either into the canonical name: it separates the family from the version ("Qwen 3.8-27B
        // with DFlash on haicluster"), and its short alias drops the dot ("alias-qwen38-27b").
        //
        builder.Rule("qwen-3.8-27b").AsSegment().InheritsFrom("qwen3.8");

        builder.Rule("qwen38-27b").AsSegment().InheritsFrom("qwen3.8");

        // The big 3.8 checkpoint reads nothing but text, and it thinks whatever it is asked:
        builder.Rule("qwen3.8-2.4t-a95b").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);
    }
}