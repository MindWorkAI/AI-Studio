using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which draw rather than write.
/// </summary>
/// <remarks>
/// Google names its image models after the chat model they grew out of and appends the word:
/// gemini-3-pro-image, gemini-3.1-flash-image, gemini-2.5-flash-image. Read as a plain substring
/// that word is far too greedy -- it sits inside "imagenet" and "reimagined" as well, and a chat
/// model carrying such a word would disappear from the user's list. As a name part it says what it
/// is meant to say, and it covers OpenAI's gpt-image-1 along the way, which is why that name is not
/// stated a second time.
/// </remarks>
public sealed class ImageGenerationModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=text-to-image", new DateOnly(2026, 9, 12), "Ported from the image generation markers of Provider/ModelKindExtensions.cs. Imagen and the Gemini image models state it in their own families as well, where the capabilities stand next to it.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("flux").AsSubstring().Kind(ModelKind.IMAGE_GENERATION);

        builder.Modifier("stable-diffusion").AsSubstring().Inherits();

        builder.Modifier("sdxl").AsSubstring().Inherits();

        builder.Modifier("dall-e").AsSubstring().Inherits();

        builder.Modifier("midjourney").AsSubstring().Inherits();

        builder.Modifier("image").AsSegment().Inherits();
    }
}