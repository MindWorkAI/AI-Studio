using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// QwQ as Model Studio serves it, which is qwq-plus.
/// </summary>
/// <remarks>
/// This is the contradiction the provider-bound rules were built for. What Model Studio sells under
/// this name is a commercial thinking-only model built on Qwen 2.5; QwQ-32B, which everybody else
/// serves, is the open-weight model. They share a family name and nothing else, and the old rules
/// could only keep them apart by living in two different functions.
///
/// Neither of them appears in Alibaba's list of models which call functions, and the model card of
/// the open weights does not mention tools at all, which is why no such ability is stated here.
/// Anybody who knows better turns it on in the expert settings.
/// </remarks>
public sealed class ModelStudioQwqFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Alibaba.cs: text in, text out, thinking which cannot be switched off, and no tools.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("qwq").AsSegment().OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);
}