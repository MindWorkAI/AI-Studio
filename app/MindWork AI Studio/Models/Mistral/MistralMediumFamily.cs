using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Medium, which could see for almost a year before it could think.
/// </summary>
public sealed class MistralMediumFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Mistral.cs: images from Mistral Medium 3 on, reasoning from Mistral Medium 3.5 on.");

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
            .Apis(CHAT_COMPLETION_API);
}