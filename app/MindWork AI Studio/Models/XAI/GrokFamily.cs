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
    public override ModelSource Source => new("https://docs.x.ai/docs/models", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from the Grok block of ProviderExtensions.OpenSource.cs. The windows come from the pricing table on the same page, which states one per model.");

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

        //
        // Grok Build is the agentic coding model behind their CLI. It reads pictures, which the
        // family fallback does not know about, and it does not think out loud.
        //
        builder.Rule("grok-build").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(256_000);

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

        //
        // The window is the one thing which differs across the 4 line, so each version states it:
        // 4.5 and 4.6 are served at 500k, while 4.3 and the whole 4.20 line are served at 1M. Plain
        // "grok-4" gets none, because xAI's table has no row for it any more.
        //
        builder.Rule("grok-4.3").AsPrefix().InheritsFrom("grok-4").ContextWindow(1_000_000);
        builder.Rule("grok-4.5").AsPrefix().InheritsFrom("grok-4").ContextWindow(500_000);
        builder.Rule("grok-4.6").AsPrefix().InheritsFrom("grok-4").ContextWindow(500_000);

        builder.Rule("grok-4.20").AsPrefix().InheritsFrom("grok-4")
            .ContextWindow(1_000_000);

        // One member of the 4.20 line answers without thinking, and it says so in its name:
        builder.Rule("grok-4.20").AsPrefix().AlsoContains("non-reasoning").Inherits()
            .Reasoning(ReasoningSupport.NONE);
    }
}