using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Large, which learned to see and to think with the same release.
/// </summary>
public sealed class MistralLargeFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/models/mistral-large-4-0", new DateOnly(2026, 10, 13), "Capabilities ported unchanged from ProviderExtensions.Mistral.cs: images and reasoning from Mistral Large 3 on. The model card of Mistral Large 3 states a 256k window, the one of Mistral Large 4 a 1M window.");

    /// <inheritdoc />
    protected override int VisionSince => 2512;

    /// <inheritdoc />
    protected override int ReasoningSince => 2512;

    /// <inheritdoc />
    protected override int LatestRelease => 2512;

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("mistral-large").AsSegment()
            .Capabilities(WHAT_THEY_COULD_ALWAYS_DO)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(256_000);

        //
        // Mistral Large 4 reads a million tokens instead of 256k. Its names carry the generation
        // instead of a release, so this rule answers for the window only; images and reasoning
        // still follow from the release, which the version names map Mistral Large 4 onto:
        //
        builder.Rule("mistral-large-4").AsSegment().InheritsFrom("mistral-large").ContextWindow(1_000_000);
    }
}