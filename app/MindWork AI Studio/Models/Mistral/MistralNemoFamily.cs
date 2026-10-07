using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Mistral NeMo, the open model Mistral built with NVIDIA.
/// </summary>
/// <remarks>
/// Mistral's own API serves it as "open-mistral-nemo", the hubs as "mistral-nemo". Whole name
/// parts cover both, which is why nothing here cares which of the two arrived.
/// </remarks>
public sealed class MistralNemoFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.OpenSource.cs: text in, text out, tool calling.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("mistral-nemo").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);
}