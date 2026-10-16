using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// The Qwen VL models, the ones built to look at pictures.
/// </summary>
/// <remarks>
/// As with the Omni series, Alibaba names only the Qwen3 VL models as function callers and the
/// older ones not at all.
/// </remarks>
public sealed class ModelStudioQwenVisionFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Alibaba.cs: images in, and tool calling from Qwen3 on.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("qwen").AsSubstring().AlsoContains("vl").OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("qwen3").AsPrefix().AlsoContains("vl").OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits()
            .Capabilities(FUNCTION_CALLING);
    }
}