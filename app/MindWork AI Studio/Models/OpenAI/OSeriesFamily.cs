using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// The o-series: o1, o3, o4 and their minis, the models which reason before they answer.
/// </summary>
/// <remarks>
/// Every one of them always reasons; what differs is how much else they can do, and the minis are
/// consistently the ones which can do less. That the mini is not simply a smaller version of its
/// generation is why each of them is stated in full: o1-mini has neither images nor tools and
/// answers only through the chat completion API, while o3-mini has tools but no images.
///
/// The minis need no ordering: their patterns are longer, so they win over the generation they
/// belong to without anybody saying which rule to try first.
/// </remarks>
public sealed class OSeriesFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.OpenAI.cs, one rule per generation and one per mini. The o1 and o3 pages state 200,000 tokens; the two cut-down minis have no page of their own, so no window is stated for them.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://github.com/openai/tiktoken/blob/main/tiktoken/model.py", new DateOnly(2026, 9, 12), "OpenAI's own mapping from model names to encodings. It maps \"o1\", \"o3\", \"o4-mini\" and the prefixes \"o1-\", \"o3-\" and \"o4-mini-\" to o200k_base, so the whole series shares one encoding.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("o1").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(RESPONSES_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(200_000)
            .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

        builder.Rule("o1-mini").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

        builder.Rule("o3").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(200_000)
            .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

        builder.Rule("o3-mini").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(RESPONSES_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

        // The one mini which is not cut down: it is the o3 generation under another number.
        builder.Rule("o4-mini").AsPrefix().InheritsFrom("o3");
    }
}