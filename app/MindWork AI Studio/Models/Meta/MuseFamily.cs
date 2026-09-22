using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Meta;

/// <summary>
/// Muse, the Meta models whose names do not say Llama.
/// </summary>
/// <remarks>
/// That is the whole reason this is a family of its own: nothing about "muse-glimmer-30b" tells the
/// Llama rules that Meta built it, and a rule for one name is cheaper than teaching them.
///
/// Glimmer always thinks. Its chat template opens the thinking channel whatever the request says,
/// and only the strength of the thinking can be turned down, so there is no mode in which it
/// answers straight away.
/// </remarks>
public sealed class MuseFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.META;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/meta-llama", new DateOnly(2026, 9, 11), "Ported unchanged from the Muse block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("muse-glimmer").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);
}