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
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://github.com/openai/tiktoken/blob/main/tiktoken/model.py", new DateOnly(2026, 9, 12), "OpenAI's own mapping from model names to encodings. It names all three of these models -- text-embedding-3-small, text-embedding-3-large and text-embedding-ada-002 -- and maps every one of them to cl100k_base rather than to the newer o200k_base of the chat models.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("text-embedding-3").AsPrefix()
            .Capabilities(TEXT_INPUT | EMBEDDING)
            .Kind(ModelKind.EMBEDDING)
            .Tokenizer(TokenizerKind.TIKTOKEN, "cl100k_base");

        builder.Rule("text-embedding-ada").AsPrefix().Inherits();
    }
}