using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// QVQ, the thinking-only model which also looks at pictures.
/// </summary>
public sealed class ModelStudioQvqFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Alibaba.cs: images in, thinking which cannot be switched off, and no tools.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("qvq").AsSegment().OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);
}