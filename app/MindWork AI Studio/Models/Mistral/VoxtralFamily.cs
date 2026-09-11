using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Voxtral, the Mistral models which listen.
/// </summary>
/// <remarks>
/// They take speech as input and answer in text, which makes them neither a chat model nor a
/// transcription model but something in between: they understand what was said rather than only
/// writing it down.
/// </remarks>
public sealed class VoxtralFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.OpenSource.cs: speech in, text out, tool calling.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("voxtral").AsSegment()
            .Capabilities(TEXT_INPUT | SPEECH_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);
}