using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Ministral, the small ones, which see from the third generation on and never reason.
/// </summary>
/// <remarks>
/// The name is one letter away from the rest of the range and shares no name part with it, which
/// the previous rules had to say out loud: the Ministral check sat above the Mistral block because
/// "ministral" does not contain "mistral". Here that is not a question anybody has to ask -- the
/// rules answer for the name part they were written for and for no other.
/// </remarks>
public sealed class MinistralFamily : MistralReleaseDatedFamily
{
    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Mistral.cs: images from Ministral 3 on, and no reasoning in any release.");

    /// <inheritdoc />
    protected override int VisionSince => 2512;

    /// <inheritdoc />
    protected override int ReasoningSince => MistralReleases.NEVER;

    /// <inheritdoc />
    protected override int LatestRelease => 2512;

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("ministral").AsSegment()
            .Capabilities(WHAT_THEY_COULD_ALWAYS_DO)
            .Apis(CHAT_COMPLETION_API);
}