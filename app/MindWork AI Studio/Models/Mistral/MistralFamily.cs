using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// The Mistral models which carry no further family name: Mistral 7B, Mistral 3, and their kin.
/// </summary>
/// <remarks>
/// The open weights are where these live. Mistral's own API sells the named ranges -- Small,
/// Medium, Large -- while the plain checkpoints are the ones people run themselves, which is why
/// nothing here is dated: those names carry a size and a quantization instead of a release.
///
/// A substring, and it has to be one: this is the fallback of the whole range, and every family
/// with a name of its own beats it by saying more. What it must not do is claim Ministral or
/// Magistral, and it does not -- neither of those two names contains "mistral".
/// </remarks>
public sealed class MistralFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/weights/", new DateOnly(2026, 9, 11), "Ported unchanged from the Mistral block of ProviderExtensions.OpenSource.cs: its default answer, and the rule for the 3 line.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("mistral").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // The 3 line reads images and thinks when it is asked to:
        builder.Rule("mistral-3").AsSegment().Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT)
            .Reasoning(ReasoningSupport.OPTIONAL);
    }
}