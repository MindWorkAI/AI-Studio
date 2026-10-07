using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// The Qwen Omni models, which take everything in and answer in text or in speech.
/// </summary>
/// <remarks>
/// Alibaba lists the Qwen3 Omni series among the models which call functions and leaves the older
/// ones off that list, which is the whole difference between the two rules below.
/// </remarks>
public sealed class ModelStudioQwenOmniFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Alibaba.cs: every modality in, text and speech out, tool calling from Qwen3 on.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("qwen").AsSubstring().AlsoContains("omni").OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | AUDIO_INPUT | SPEECH_INPUT | VIDEO_INPUT | TEXT_OUTPUT | SPEECH_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("qwen3").AsPrefix().AlsoContains("omni").OnlyOn(LLMProviders.ALIBABA_CLOUD).Inherits()
            .Capabilities(FUNCTION_CALLING);
    }
}