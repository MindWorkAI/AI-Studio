using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which put search results back into order.
/// </summary>
/// <remarks>
/// A reranker is almost always named after the embedding model it belongs to: bge-reranker sits
/// next to bge, gte-multilingual-reranker next to gte, Qwen3-VL-Reranker next to Qwen3-VL-Embedding.
/// So nearly every one of these names carries an embedding marker as well, and the computed
/// specificity has no way of knowing which of the two statements is the one about the model itself.
/// This is the one place where the order of asking is the knowledge, which is what the explicit rank
/// is for.
/// </remarks>
public sealed class RerankingModelsFamily : ModelFamily
{
    /// <summary>
    /// Why this outranks every statement about embedding models.
    /// </summary>
    private const string NAMED_AFTER_THE_EMBEDDING_MODEL = "A reranker carries the name of the embedding model it reorders for, so the embedding markers match it too. Which of them is right cannot be worked out of the text.";

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=text-ranking", new DateOnly(2026, 9, 12), "Ported from the reranking markers of Provider/ModelKindExtensions.cs, where the same precedence was written as the order of two if statements.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Modifier("rerank").AsSubstring()
            .Rank(1, NAMED_AFTER_THE_EMBEDDING_MODEL)
            .Kind(ModelKind.RERANKING);
}