using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which turn text into a vector, whoever built them.
/// </summary>
/// <remarks>
/// Everything in this folder answers one question: what is a model made for, as opposed to what can
/// it do. The two used to be answered by two different pieces of code walking the same name, and
/// before that by every provider carrying a list of name fragments of its own -- lists which
/// disagreed, so that nomic-embed-text was an embedding model at one provider and a chat model at
/// the next.
///
/// These are modifiers rather than selectors, and that is the whole trick. A model keeps the family
/// it belongs to and this only says what it is for: llama-guard stays a Llama, and an embedding
/// checkpoint of a family we have rules for keeps those rules. Written as selectors they would have
/// to win against the family, and "embed" against "llama" is a contest neither of them should be
/// in -- both are five characters of substring, which is a tie, which is an error.
///
/// What none of them may become is a place for provider-specific knowledge. That "codestral" fills
/// in the middle at Mistral is true for Mistral; such a statement belongs to the family.
/// </remarks>
public sealed class EmbeddingModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/models?pipeline_tag=feature-extraction", new DateOnly(2026, 9, 12), "Ported from the embedding markers of Provider/ModelKindExtensions.cs. The e5 line says it in its own family, so it is not repeated here.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("embed").AsSubstring().Kind(ModelKind.EMBEDDING);

        builder.Modifier("bge").AsSubstring().Inherits();

        builder.Modifier("mpnet").AsSubstring().Inherits();

        builder.Modifier("paraphrase").AsSubstring().Inherits();

        //
        // The one marker which was really an organization rather than a model. It still holds where
        // a name arrives whole, but the host takes the organization off before any rule sees the
        // name, so the model this organization is known for has to stand next to it: all-MiniLM-L6-v2
        // says nothing about embedding except through who published it.
        //
        builder.Modifier("sentence-transformers").AsSubstring().Inherits();

        builder.Modifier("minilm").AsSubstring().Inherits();

        builder.Modifier("gritlm").AsSubstring().Inherits();

        // General Text Embeddings, from Alibaba. Written as a name part rather than as a substring,
        // because three letters appear inside far too many unrelated words:
        builder.Modifier("gte").AsSegment().Inherits();
    }
}