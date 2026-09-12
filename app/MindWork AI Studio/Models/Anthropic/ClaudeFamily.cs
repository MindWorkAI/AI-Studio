using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Anthropic;

/// <summary>
/// Claude, all of it: the 3.x models, the 4.x models, and the 5 line.
/// </summary>
/// <remarks>
/// One family, because every Claude is the same shape and always has been -- text and images in,
/// text out, tool calling, one API. What each generation adds to that is a single sentence about
/// thinking, and the rules below are almost nothing but those sentences.
///
/// The first rule is the family's own fallback, and it is a statement rather than an accident: a
/// Claude nobody has written a rule for yet is still a Claude, and every one of them so far reads
/// images and calls tools. It answers for whole name parts, so every rule bound to the start of a
/// name beats it, whatever their lengths -- which is what lets it sit first and mean "unless".
/// </remarks>
public sealed class ClaudeFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ANTHROPIC;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.claude.com/docs/en/build-with-claude/context-windows", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.Anthropic.cs: one shape for all of Claude, and one sentence per generation about how it thinks. The context window page names the models with a 1M window and says every other Claude has 200k.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        //
        // 200k is the window of every Claude which is not named on the list of the 1M ones, which is
        // how Anthropic states it themselves: the page names the exceptions and says "other Claude
        // models" for the rest. So the fallback carries it, and the generations which got the larger
        // window say so one by one below.
        //
        builder.Rule("claude").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(200_000);

        //
        // The 3.x models say nothing beyond the shape above, so nothing is written for them: the
        // previous rules had a branch for "claude-3-" which returned exactly what its fallback
        // returned. Only 3.7 differs, by being the first Claude which could be asked to think.
        //
        builder.Rule("claude-3-7").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.OPTIONAL);

        // The 4.x models think when a thinking budget is given, and not otherwise:
        builder.Rule("claude-opus-4").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("claude-sonnet-4").AsPrefix().InheritsFrom("claude-opus-4");
        builder.Rule("claude-haiku-4-5").AsPrefix().InheritsFrom("claude-opus-4");

        //
        // Where the window grew inside the 4 line. These rules say nothing but the number: Opus 4.6
        // through 4.8 and Sonnet 4.6 have the 1M window, while the 4.0, 4.1 and 4.5 models of the
        // same prefixes keep the 200k they were released with.
        //
        builder.Rule("claude-opus-4-6").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(1_000_000);
        builder.Rule("claude-opus-4-7").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(1_000_000);
        builder.Rule("claude-opus-4-8").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(1_000_000);
        builder.Rule("claude-sonnet-4-6").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(1_000_000);

        // Opus 5 and Sonnet 5 think adaptively unless thinking is turned off:
        builder.Rule("claude-opus-5").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT)
            .ContextWindow(1_000_000);

        builder.Rule("claude-sonnet-5").AsPrefix().InheritsFrom("claude-opus-5");

        // Fable 5 and Mythos 5 always think, and there is no switch for it:
        builder.Rule("claude-fable-5").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(1_000_000);

        builder.Rule("claude-mythos-5").AsPrefix().InheritsFrom("claude-fable-5");
    }
}