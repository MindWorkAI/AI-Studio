using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which listen and write down what they heard.
/// </summary>
/// <remarks>
/// Whisper and Voxtral are missing here on purpose: both have a family of their own, where the
/// statement that they transcribe stands next to what they can do. Repeating it here would be a
/// second place to keep it right.
/// </remarks>
public sealed class TranscriptionModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=automatic-speech-recognition", new DateOnly(2026, 9, 12), "Ported from the transcription markers of Provider/ModelKindExtensions.cs, minus the two which their own families now state.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // OpenAI appends it to the model it grew out of: gpt-4o-transcribe, gpt-4o-mini-transcribe.
        builder.Modifier("transcribe").AsSegment().Kind(ModelKind.TRANSCRIPTION);

        builder.Modifier("wav2vec").AsSubstring().Inherits();

        builder.Modifier("parakeet").AsSubstring().Inherits();
    }
}