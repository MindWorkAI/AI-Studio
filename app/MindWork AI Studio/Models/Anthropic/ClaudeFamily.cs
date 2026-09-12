using AIStudio.Models.Matching;

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
///
/// The one thing no rule below states is how many images a Claude takes. Anthropic ties that number
/// to the context window instead of to the model, so it is worked out afterwards rather than written
/// on every line which sets a window.
/// </remarks>
public sealed class ClaudeFamily : ModelFamily
{
    /// <summary>
    /// The window every Claude has unless its own rule states the larger one.
    /// </summary>
    private const int STANDARD_WINDOW = 200_000;

    /// <summary>
    /// The window of the Claude models which read a million tokens.
    /// </summary>
    private const int LARGE_WINDOW = 1_000_000;

    /// <summary>
    /// How many images one request may carry when the model has the standard window.
    /// </summary>
    private const int IMAGES_PER_REQUEST_STANDARD_WINDOW = 100;

    /// <summary>
    /// How many images one request may carry for every other Claude.
    /// </summary>
    private const int IMAGES_PER_REQUEST_OTHERWISE = 600;

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ANTHROPIC;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.claude.com/docs/en/build-with-claude/context-windows", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.Anthropic.cs: one shape for all of Claude, and one sentence per generation about how it thinks. The context window page names the models with a 1M window and says every other Claude has 200k.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://platform.claude.com/docs/en/build-with-claude/vision", new DateOnly(2026, 9, 12), "The vision page gives the image limit as a rule rather than as a number per model: 100 images per request on the API for models with a 200k-token context window, 600 per request for all other models. The 20 it also names belongs to claude.ai, not to the API."),
        new("https://platform.claude.com/docs/en/build-with-claude/token-counting", new DateOnly(2026, 9, 12), "Anthropic publishes no tokenizer file at all; they count through the /v1/messages/count_tokens endpoint instead. The same page warns that Claude 4.7 and later use a newer tokenizer, on which the same text counts roughly 30 percent higher -- so AI Studio's built-in estimate is further off for those models than for the older ones.")
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Anthropic states no image limit per model. They state a rule which reads off the context
    /// window, and this is that rule -- written once rather than repeated as a number on every line
    /// which sets a window. Two statements of one fact drift apart, and the way they drift here is
    /// silent: the next Claude with the larger window would quietly keep the smaller limit because
    /// somebody wrote one number and not the other.
    /// </remarks>
    public override ModelProfile Refine(in ModelId id, in ModelProfile selected)
    {
        if (!selected.Context.IsKnown)
            return selected;

        var perRequest = selected.Context.DefaultTokens is STANDARD_WINDOW ? IMAGES_PER_REQUEST_STANDARD_WINDOW : IMAGES_PER_REQUEST_OTHERWISE;
        return selected with { Images = new ImageLimits(null, perRequest) };
    }

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
            .ContextWindow(STANDARD_WINDOW)
            .Tokenizer(TokenizerKind.PROVIDER_API, "/v1/messages/count_tokens");

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
        builder.Rule("claude-opus-4-6").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(LARGE_WINDOW);
        builder.Rule("claude-opus-4-7").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(LARGE_WINDOW);
        builder.Rule("claude-opus-4-8").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(LARGE_WINDOW);
        builder.Rule("claude-sonnet-4-6").AsPrefix().InheritsFrom("claude-opus-4").ContextWindow(LARGE_WINDOW);

        // Opus 5 and Sonnet 5 think adaptively unless thinking is turned off:
        builder.Rule("claude-opus-5").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT)
            .ContextWindow(LARGE_WINDOW);

        builder.Rule("claude-sonnet-5").AsPrefix().InheritsFrom("claude-opus-5");

        // Fable 5 and Mythos 5 always think, and there is no switch for it:
        builder.Rule("claude-fable-5").AsPrefix().InheritsFrom("claude")
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(LARGE_WINDOW);

        builder.Rule("claude-mythos-5").AsPrefix().InheritsFrom("claude-fable-5");
    }
}