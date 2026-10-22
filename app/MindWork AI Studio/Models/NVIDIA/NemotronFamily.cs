using static AIStudio.Provider.Capability;

namespace AIStudio.Models.NVIDIA;

/// <summary>
/// Nemotron, which NVIDIA builds for agentic work and mostly out of somebody else's weights.
/// </summary>
/// <remarks>
/// That last part is what the rules have to get right. Llama-3.3-Nemotron-Super carries two family
/// names, and the previous rules answered it as a Llama for no better reason than that the Llama
/// block stood higher up in the file. What NVIDIA changed about those weights is exactly the part
/// the answer is about: the thinking switch and the tool template. Here the name part wins over the
/// substring, so the model is answered by the family which made it what it is.
///
/// Every generation is text only. The point releases carry a line of their own because a dot
/// separates versions rather than name parts, so "nemotron-3" does not answer for "nemotron-3.5".
/// </remarks>
public sealed class NemotronFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.NVIDIA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/nvidia", new DateOnly(2026, 9, 11), "Ported unchanged from the Nemotron block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // The earlier generations have to be asked to think:
        builder.Rule("nemotron").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        // The third one thinks unless the request says otherwise, through enable_thinking=False:
        builder.Rule("nemotron-3").AsSegment().Inherits()
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("nemotron-3.5").AsSegment().Inherits();
    }
}