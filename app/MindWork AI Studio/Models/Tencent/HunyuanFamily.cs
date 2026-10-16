using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Tencent;

/// <summary>
/// Hunyuan, from Tencent.
/// </summary>
/// <remarks>
/// The short name needs a rule of its own because that is how the model arrives: several providers
/// serve it as "tencent/hy3", so looking at the start of the name finds nothing.
///
/// Hy3 answers straight away unless it is asked to think. Its reasoning_effort parameter starts at
/// no_think, and low and high have to be requested.
/// </remarks>
public sealed class HunyuanFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.TENCENT;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/tencent", new DateOnly(2026, 9, 11), "Ported unchanged from the Hunyuan block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("hunyuan").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("hy3").AsSegment().Inherits();
    }
}