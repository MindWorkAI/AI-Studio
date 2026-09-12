using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Microsoft;

/// <summary>
/// E5, the embedding models built on somebody else's weights.
/// </summary>
/// <remarks>
/// "e5-mistral-7b-instruct" is what made this a family of its own. It is an embedding model, and it
/// carries the name of the model it was trained from, so the Mistral rules answer for it and tell
/// it that it chats and calls functions. Saying which name means what it says is cheaper than
/// teaching every family whose weights somebody built an embedder from.
///
/// The E5 part is the whole statement: the rest of the name says nothing about what the model does.
/// </remarks>
public sealed class E5Family : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MICROSOFT;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/intfloat/e5-mistral-7b-instruct", new DateOnly(2026, 9, 11), "The app lists this under IProvider.GetEmbeddingModels, which is where the statement that it embeds comes from.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("e5").AsSegment()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);
}