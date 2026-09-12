using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// gpt-oss, the weights OpenAI published.
/// </summary>
/// <remarks>
/// The only OpenAI model anybody else may serve, and the reason the rest of this folder does not
/// have to worry about being confused with it: "gpt-oss" is a name part of its own, while every
/// cloud model of theirs carries a version behind the "gpt". The previous rules needed a function
/// to tell the two apart, and it is the specificity which does it here.
///
/// It browses through the harmony format it was trained on, which is why web search is stated even
/// though nothing else among the open weights has it.
/// </remarks>
public sealed class GptOssFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/openai/gpt-oss-120b", new DateOnly(2026, 9, 11), "Ported unchanged from the gpt-oss check of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("gpt-oss").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(CHAT_COMPLETION_API);
}