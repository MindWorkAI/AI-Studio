using static AIStudio.Provider.Capability;

namespace AIStudio.Models.XAI;

/// <summary>
/// Grok, from the old vision models to the 5 line.
/// </summary>
/// <remarks>
/// The family's own fallback calls functions, and that is deliberate: without it an unknown Grok
/// version would reach whatever answers for everything and lose tool calling, which every Grok
/// since the 3 line has. Grok 3 itself needs no rule for the same reason -- the fallback already
/// says exactly what it is.
///
/// Video is not among their modalities. xAI serves audio, image, and video through models and APIs
/// of their own, and the model pages of the 4.x line say "text, image" and nothing else.
/// </remarks>
public sealed class GrokFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.XAI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.x.ai/docs/models", new DateOnly(2026, 9, 11), "Ported unchanged from the Grok block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("grok").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // The old vision models look at pictures and call nothing:
        builder.Rule("grok").AsSegment().AlsoContains("vision")
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("grok-3-mini").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        //
        // The 4 line reads images and always thinks; only the effort can be set. The 4.20 models
        // need a line of their own because a dot separates versions rather than name parts, so
        // "grok-4" does not answer for "grok-4.20".
        //
        builder.Rule("grok-4").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("grok-4.20").AsPrefix().InheritsFrom("grok-4");

        // One member of the 4.20 line answers without thinking, and it says so in its name:
        builder.Rule("grok-4.20").AsPrefix().AlsoContains("non-reasoning").Inherits()
            .Reasoning(ReasoningSupport.NONE);
    }
}