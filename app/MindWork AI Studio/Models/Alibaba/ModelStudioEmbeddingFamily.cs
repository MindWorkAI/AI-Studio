using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Alibaba;

/// <summary>
/// Alibaba's embedding models, which turn text into a vector and answer nothing.
/// </summary>
/// <remarks>
/// The previous rules answered for these with the Model Studio default and told them they call
/// functions. The prefix is Alibaba's own: the app filters the catalog by "text-embedding-" to find
/// them, which is also why the rule may be written that broadly -- bound to this provider, it can
/// only ever meet the models Alibaba names that way.
/// </remarks>
public sealed class ModelStudioEmbeddingFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.ALIBABA;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.alibabacloud.com/help/en/model-studio/embedding", new DateOnly(2026, 9, 11), "Provider/AlibabaCloud/ProviderAlibabaCloud.cs adds these in GetEmbeddingModels and filters the catalog by the prefix \"text-embedding-\".");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("text-embedding").AsPrefix().OnlyOn(LLMProviders.ALIBABA_CLOUD)
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);
}