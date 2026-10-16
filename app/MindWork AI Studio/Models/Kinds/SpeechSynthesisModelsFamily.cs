using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which speak.
/// </summary>
/// <remarks>
/// Besides the pure text-to-speech models this covers the ones which answer in audio, such as
/// gpt-audio and gpt-4o-audio-preview. Those do accept a text-only request, but they are made for
/// spoken conversations, and the providers offering them keep them out of their chat model lists as
/// well.
///
/// All three words are stated as name parts. The markers they replace carried a hyphen on one side
/// to say the same thing, which caught one name these do not: Coqui's XTTS glues the word to an x.
/// It is named outright rather than loosening all three into substrings, where "tts" would be three
/// characters claiming every name that happens to contain them.
/// </remarks>
public sealed class SpeechSynthesisModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=text-to-speech", new DateOnly(2026, 9, 12), "Ported from the speech synthesis markers of Provider/ModelKindExtensions.cs, where each of the three was written twice to allow for a separator on either side.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("tts").AsSegment().Kind(ModelKind.SPEECH_SYNTHESIS);

        builder.Modifier("xtts").AsSegment().Inherits();

        builder.Modifier("speech").AsSegment().Inherits();

        builder.Modifier("audio").AsSegment().Inherits();
    }
}