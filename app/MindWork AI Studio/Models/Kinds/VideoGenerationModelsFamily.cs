using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which make video.
/// </summary>
/// <remarks>
/// Two of these names have to stand as a name part of their own. "kling" taken as a plain substring
/// also matches the organization Klingspor, the model Inkling, and the fine-tune
/// Llama-2-7b-chat-klingon -- all of them models to chat with, which would vanish from the user's
/// list. The models themselves are called kling-v1 and kling-video, where the name ends at a
/// separator. Google's veo is the same story with an even shorter word.
/// </remarks>
public sealed class VideoGenerationModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=text-to-video", new DateOnly(2026, 9, 12), "Ported from the video generation markers of Provider/ModelKindExtensions.cs, where veo carried a trailing hyphen to say the same thing a name part says here. Grok Imagine was added after it turned up in the chat list while testing; see https://docs.x.ai/docs/models.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("sora").AsSubstring().Kind(ModelKind.VIDEO_GENERATION);

        builder.Modifier("runway").AsSubstring().Inherits();

        builder.Modifier("hailuo").AsSubstring().Inherits();

        builder.Modifier("veo").AsSegment().Inherits();

        builder.Modifier("kling").AsSegment().Inherits();

        // Grok Imagine makes both stills and film; the word next to it says which:
        builder.Modifier("grok-imagine").AsSegment().AlsoContains("video").Inherits();
    }
}