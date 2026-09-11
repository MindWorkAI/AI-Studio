using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// OpenAI's embedding models, which turn text into a vector and answer nothing.
/// </summary>
/// <remarks>
/// The previous rules had no idea these existed. They fell through to the OpenAI fallback and were
/// told they see images, call functions, and search the web -- an answer with nothing right about
/// it, for models the app asks for through a separate method of its own.
///
/// The generation is named rather than the prefix "text-embedding": Google and Alibaba Cloud name
/// their own embedding models the same way, and those are their models, not these.
/// </remarks>
public sealed class OpenAIEmbeddingFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/guides/embeddings", new DateOnly(2026, 9, 11), "The app lists these under IProvider.GetEmbeddingModels, which is where the statement that they embed comes from.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("text-embedding-3").AsPrefix()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING);

        builder.Rule("text-embedding-ada").AsPrefix().Inherits();
    }
}