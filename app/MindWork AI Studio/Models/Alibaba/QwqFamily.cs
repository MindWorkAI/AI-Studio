using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// QwQ as everybody except Alibaba Cloud serves it: the open weights built on Qwen 2.5.
/// </summary>
/// <remarks>
/// The other half of the contradiction the provider-bound rules exist for. What Model Studio sells
/// as "qwq-plus" is a commercial model; QwQ-32B, which the gateways and the local engines serve, is
/// the published checkpoint. The two share a family name and nothing else.
///
/// Both answer the same here, and for the same reason: neither the model card nor Alibaba's list of
/// models which call functions mentions tools at all. Anybody who knows better says so in the
/// expert settings.
/// </remarks>
public sealed class QwqFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/Qwen/QwQ-32B", new DateOnly(2026, 9, 11), "Ported unchanged from the QwQ check of ProviderExtensions.OpenSource.cs: text in, text out, thinking which cannot be switched off, and no tools.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("qwq").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);
}