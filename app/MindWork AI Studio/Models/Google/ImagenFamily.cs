using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// Imagen, which draws a picture from a description and does nothing else.
/// </summary>
/// <remarks>
/// The previous rules had no branch for it. Its name does not contain "gemini", so it fell to the
/// last line of the Google function and was answered as a chat model: reads images, writes text,
/// calls functions. Not one of the three is true, and the one thing it does -- writing an image --
/// was not said at all.
///
/// Whole name parts, not a substring: "imagen" also sits inside "imagenet" and "reimagined", and a
/// chat model carrying such a word would be turned into an image generator by a careless match.
/// </remarks>
public sealed class ImagenFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/imagen", new DateOnly(2026, 9, 11), "A description goes in and an image comes out; there is no conversation and no tool calling.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("imagen").AsSegment()
            .Capabilities(TEXT_INPUT | IMAGE_OUTPUT)
            .Kind(ModelKind.IMAGE_GENERATION);
}