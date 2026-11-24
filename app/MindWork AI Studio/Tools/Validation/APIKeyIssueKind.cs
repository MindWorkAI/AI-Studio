namespace AIStudio.Tools.Validation;

/// <summary>
/// What keeps an API key from being sent, as far as its characters tell.
/// </summary>
public enum APIKeyIssueKind
{
    NONE,

    /// <summary>
    /// A space, a tab, or a line break inside the key.
    /// </summary>
    /// <remarks>
    /// No provider issues keys with whitespace, so the user most likely copied more than the key,
    /// e.g., two lines of a table or the key together with its name. Values which people choose
    /// themselves, such as a password, may contain a plain space, but no other whitespace.
    /// </remarks>
    WHITESPACE,

    /// <summary>
    /// A character nobody sees, e.g., a zero-width space, a byte order mark, or a control character.
    /// </summary>
    /// <remarks>
    /// Web pages and documents add such characters to what they show, and they come along when the
    /// user copies the key from there.
    /// </remarks>
    INVISIBLE_CHARACTER,

    /// <summary>
    /// A visible character outside of ASCII, e.g., an umlaut or a typographic quotation mark.
    /// </summary>
    NON_ASCII_CHARACTER,
}