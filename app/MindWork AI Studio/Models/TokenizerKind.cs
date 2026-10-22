namespace AIStudio.Models;

/// <summary>
/// What sort of tokenizer a model uses, and therefore how its name would have to be resolved.
/// </summary>
/// <remarks>
/// A bare name would be a lie. The runtime loads Hugging Face tokenizer.json files, OpenAI names
/// tiktoken encodings such as o200k_base, and Anthropic and Google publish no tokenizer at all but
/// offer an API which counts for you. Without the kind next to the name, somebody would eventually
/// try to fetch "o200k_base" from a model hub.
/// </remarks>
public enum TokenizerKind
{
    /// <summary>
    /// We have no statement about this model's tokenizer, so the built-in default one is used.
    /// </summary>
    UNKNOWN,

    /// <summary>
    /// A repository on the Hugging Face hub which ships a tokenizer.json.
    /// </summary>
    HUGGING_FACE,

    /// <summary>
    /// A tiktoken encoding, named the way OpenAI names it.
    /// </summary>
    TIKTOKEN,

    /// <summary>
    /// The vendor counts tokens through an API of its own instead of publishing a tokenizer.
    /// </summary>
    PROVIDER_API,

    /// <summary>
    /// The model has no tokenizer to speak of, such as an image or audio model.
    /// </summary>
    NONE,
}