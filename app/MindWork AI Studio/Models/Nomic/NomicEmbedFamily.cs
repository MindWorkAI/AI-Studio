using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Nomic;

/// <summary>
/// The Nomic embedding models, which everybody runs locally and nobody chats with.
/// </summary>
/// <remarks>
/// One of the most widely served models there is: it is what a local setup reaches for when it
/// needs vectors. The previous rules had no idea it existed, so it fell into the assumption that an
/// unknown model chats and calls functions -- three statements about a model which does none of
/// them, and the one thing it does was not said at all.
/// </remarks>
public sealed class NomicEmbedFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.NOMIC_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/nomic-ai", new DateOnly(2026, 9, 11), "The app lists these under IProvider.GetEmbeddingModels, which is where the statement that they embed comes from.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("nomic-embed").AsSubstring()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);
}