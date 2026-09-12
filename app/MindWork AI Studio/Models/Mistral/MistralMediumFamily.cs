using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Medium, which could see for almost a year before it could think.
/// </summary>
public sealed class MistralMediumFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/models/model-cards/mistral-medium-3-5-26-04", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.Mistral.cs: images from Mistral Medium 3 on, reasoning from Mistral Medium 3.5 on. The model card states a 256k window.");

    /// <inheritdoc />
    protected override int VisionSince => 2505;

    /// <inheritdoc />
    protected override int ReasoningSince => 2604;

    /// <inheritdoc />
    protected override int LatestRelease => 2604;

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("mistral-medium").AsSegment()
            .Capabilities(WHAT_THEY_COULD_ALWAYS_DO)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(256_000);
}