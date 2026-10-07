using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// The Gemini chat models.
/// </summary>
/// <remarks>
/// A Gemini reads everything -- text, images, audio, speech, video -- writes text, and calls tools.
/// That is the first rule, and it is the family's own fallback for a Gemini nobody has written a
/// rule for yet. What the generations add to it is how they think, and the older exceptions take
/// something away instead.
///
/// Every generation gets a line of its own, including the dotted ones. The dot is a version
/// boundary rather than a name part boundary, deliberately -- it is what keeps llama3 and llama3.1
/// apart -- so a rule for "gemini-3" does not answer for "gemini-3.1", and each has to say so
/// itself. The previous rules searched for "gemini-3" anywhere in the name and covered unreleased
/// versions by accident; the price of not doing that is a line per generation, and the verification
/// run names any model of the corpus which finds no rule.
/// </remarks>
public sealed class GeminiFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/gemini-3", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.Google.cs: one shape for all of Gemini, one sentence per generation about thinking. The Gemini 3 guide states a one million token input window; the 2.5 model pages state their input limit as 1,048,576, and both numbers are written here as their page gives them.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://ai.google.dev/gemini-api/docs/image-understanding", new DateOnly(2026, 9, 12), "States one number for the whole family: \"Gemini models support a maximum of 3,600 image files per request.\" The 20 MB it also names is a limit on the request body rather than on the number of images."),
        new("https://ai.google.dev/gemini-api/docs/tokens", new DateOnly(2026, 9, 12), "Google publishes no tokenizer file for Gemini. Counting happens through the countTokens method of the API, which returns the number of tokens of the input alone.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        //
        // Google states the image limit once, for all of Gemini, so it sits on the fallback and
        // every generation inherits it. The two rules below which do not inherit from here say
        // nothing about it: the live model looks at no still images at all, and for the 1.0 vision
        // model Google's current pages state no number any more.
        //
        builder.Rule("gemini").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | AUDIO_INPUT | SPEECH_INPUT | VIDEO_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Images(maxPerRequest: 3_600)
            .Tokenizer(TokenizerKind.PROVIDER_API, "countTokens");

        // The one Gemini which only ever read text and images:
        builder.Rule("gemini-1.0-pro-vision").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        //
        // The live model, which belongs to a different API: it speaks back, and it is the one
        // Gemini that does not look at still images.
        //
        builder.Rule("gemini-2.0-flash-live").AsPrefix()
            .Capabilities(TEXT_INPUT | AUDIO_INPUT | SPEECH_INPUT | VIDEO_INPUT | TEXT_OUTPUT | SPEECH_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        //
        // Google states an input limit and an output limit rather than one window. The input limit
        // is the one a conversation is measured against, because that is where the conversation
        // accumulates, so that is the number written here.
        //
        builder.Rule("gemini-2.5").AsPrefix().InheritsFrom("gemini")
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(1_048_576);

        //
        // The one exception of the 2.5 line: it can think, but only when asked. From the 3.x line
        // on, even the Flash Lite models think at their lowest level.
        //
        builder.Rule("gemini-2.5-flash-lite").AsPrefix().InheritsFrom("gemini-2.5")
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("gemini-3").AsPrefix().InheritsFrom("gemini")
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(1_000_000);

        builder.Rule("gemini-3.1").AsPrefix().InheritsFrom("gemini-3");
        builder.Rule("gemini-3.7").AsPrefix().InheritsFrom("gemini-3");

        // The two rolling aliases, which carry no version number and point at the current line:
        builder.Rule("gemini-flash-latest").AsExact().InheritsFrom("gemini-3");
        builder.Rule("gemini-pro-latest").AsExact().InheritsFrom("gemini-3");
    }
}