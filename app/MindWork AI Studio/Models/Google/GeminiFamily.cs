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
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Google.cs: one shape for all of Gemini, one sentence per generation about thinking.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gemini").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | AUDIO_INPUT | SPEECH_INPUT | VIDEO_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

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

        builder.Rule("gemini-2.5").AsPrefix().InheritsFrom("gemini")
            .Reasoning(ReasoningSupport.ALWAYS);

        //
        // The one exception of the 2.5 line: it can think, but only when asked. From the 3.x line
        // on, even the Flash Lite models think at their lowest level.
        //
        builder.Rule("gemini-2.5-flash-lite").AsPrefix().InheritsFrom("gemini")
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("gemini-3").AsPrefix().InheritsFrom("gemini")
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("gemini-3.1").AsPrefix().InheritsFrom("gemini-3");
        builder.Rule("gemini-3.7").AsPrefix().InheritsFrom("gemini-3");

        // The two rolling aliases, which carry no version number and point at the current line:
        builder.Rule("gemini-flash-latest").AsExact().InheritsFrom("gemini-3");
        builder.Rule("gemini-pro-latest").AsExact().InheritsFrom("gemini-3");
    }
}