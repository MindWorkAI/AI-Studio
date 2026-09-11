using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Small, which gained images with 3.1 and reasoning with 4.
/// </summary>
public sealed class MistralSmallFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Mistral.cs: images from Mistral Small 3.1 on, reasoning from Mistral Small 4 on.");

    /// <inheritdoc />
    protected override int VisionSince => 2503;

    /// <inheritdoc />
    protected override int ReasoningSince => 2603;

    /// <inheritdoc />
    protected override int LatestRelease => 2603;

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("mistral-small").AsSegment()
            .Capabilities(WHAT_THEY_COULD_ALWAYS_DO)
            .Apis(CHAT_COMPLETION_API);
}