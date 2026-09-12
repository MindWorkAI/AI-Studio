using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Large, which learned to see and to think with the same release.
/// </summary>
public sealed class MistralLargeFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/models/mistral-large-3-25-12", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.Mistral.cs: images and reasoning from Mistral Large 3 on. The model card states a 256k window.");

    /// <inheritdoc />
    protected override int VisionSince => 2512;

    /// <inheritdoc />
    protected override int ReasoningSince => 2512;

    /// <inheritdoc />
    protected override int LatestRelease => 2512;

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("mistral-large").AsSegment()
            .Capabilities(WHAT_THEY_COULD_ALWAYS_DO)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(256_000);
}