namespace AIStudio.Models;

/// <summary>
/// Points at the tokenizer a model uses, without fetching it.
/// </summary>
/// <remarks>
/// Only the reference is recorded here. Obtaining a tokenizer is a feature of its own, and today
/// only the Hugging Face kind could be resolved at all; the other kinds document what would have to
/// happen. Unknown means the built-in default tokenizer, which is what every model uses today.
/// </remarks>
/// <param name="Kind">What sort of tokenizer this is, which decides how the name would be resolved.</param>
/// <param name="Id">The name, in whatever spelling the kind uses. Meaningless unless the reference is known.</param>
public readonly record struct TokenizerRef(TokenizerKind Kind, string Id)
{
    /// <summary>
    /// The tokenizer of a model we have no statement about: the built-in default one.
    /// </summary>
    public static readonly TokenizerRef UNKNOWN = new(TokenizerKind.UNKNOWN, string.Empty);

    /// <summary>
    /// Whether this reference names something. Read the ID only when it does.
    /// </summary>
    public bool IsKnown => this.Kind is not TokenizerKind.UNKNOWN && !string.IsNullOrWhiteSpace(this.Id);
}