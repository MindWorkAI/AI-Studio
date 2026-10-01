using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral Saba, the regional model for the Middle East and South Asia.
/// </summary>
/// <remarks>
/// The one Mistral in this range which calls no tools at all. It needs its own rule for that
/// reason alone: without it, the length of "mistral-small" and "mistral-large" would not matter,
/// but the shape they share would be handed to a model which does not have it.
/// </remarks>
public sealed class MistralSabaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Mistral.cs: text in, text out, and nothing else.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("mistral-saba").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);
}