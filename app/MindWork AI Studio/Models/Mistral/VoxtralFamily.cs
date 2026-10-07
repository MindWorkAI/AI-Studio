using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Voxtral, the Mistral models which listen.
/// </summary>
/// <remarks>
/// They take speech as input and answer in text, which makes them neither a chat model nor a
/// transcription model but something in between: they understand what was said rather than only
/// writing it down.
///
/// The app has to pick one of the two all the same, and the provider decides it: asking Mistral for
/// a chat completion with voxtral-mini-latest is answered with "Invalid model". So they are
/// transcription models, which is what keeps them out of the chat list, while the capabilities above
/// still say what they understand.
/// </remarks>
public sealed class VoxtralFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.OpenSource.cs: speech in, text out, tool calling. That they count as transcription models comes from Provider/ModelKindExtensions.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("voxtral").AsSegment()
            .Capabilities(TEXT_INPUT | SPEECH_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Kind(ModelKind.TRANSCRIPTION);
}