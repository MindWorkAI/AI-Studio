using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// Whisper, which listens and writes down what it heard.
/// </summary>
/// <remarks>
/// OpenAI built it and released the weights, so it turns up far beyond OpenAI's own API: Fireworks,
/// the GWDG, and Groq all serve a Whisper. This family is bound to no provider for that reason --
/// it is the same model wherever it runs, and the previous rules answered for it at every one of
/// those places with the global fallback, tool calling included.
/// </remarks>
public sealed class WhisperFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/guides/speech-to-text", new DateOnly(2026, 9, 11), "The app lists these under IProvider.GetTranscriptionModels, which is where the statement that they transcribe comes from.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("whisper").AsSegment()
            .Capabilities(SPEECH_INPUT | TEXT_OUTPUT)
            .Kind(ModelKind.TRANSCRIPTION);
}