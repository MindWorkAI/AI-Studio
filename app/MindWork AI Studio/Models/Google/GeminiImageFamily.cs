using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// The Gemini models which draw as well as write.
/// </summary>
/// <remarks>
/// They are named like every other Gemini, with a version and a size, and the only thing setting
/// them apart is the name part "image". So the rules here are the generation rules of the chat
/// family with that one part required on top, and requiring it is exactly what makes them win: two
/// rules reaching equally far into a name are separated by how many conditions they carry.
///
/// What they can do is nearly the opposite of what their generation can. They write images, which
/// no chat Gemini does, and they call no tools, which every chat Gemini does. Reading them as chat
/// models of their line -- which is what happens when nobody asks about the image part first --
/// promises tool calling that is not there.
/// </remarks>
public sealed class GeminiImageFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/image-generation", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Google.cs: images out, no tool calling, and thinking from the 3 line on.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gemini-2.5").AsPrefix().AlsoContains("image")
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | IMAGE_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        // From the 3 line on they think about a complicated prompt, and it cannot be switched off:
        builder.Rule("gemini-3").AsPrefix().AlsoContains("image").Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);

        // Only the 3.1 Flash image models watch video:
        builder.Rule("gemini-3.1").AsPrefix().AlsoContains("image").Inherits()
            .Capabilities(VIDEO_INPUT);
    }
}