namespace AIStudio.Settings;

/// <summary>
/// What the values of a glossary mean. Every glossary maps terms to values; the type tells the model how to use them.
/// </summary>
public enum GlossaryType
{
    /// <summary>
    /// The value is the fixed translation of the term, from the source into the target language of the glossary.
    /// </summary>
    TRANSLATION,

    /// <summary>
    /// The value explains the term, e.g. a technical term of the organization or an invented word of a book.
    /// </summary>
    REFERENCE,
}