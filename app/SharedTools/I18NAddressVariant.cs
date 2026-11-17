namespace SharedTools;

/// <summary>
/// Names the keys of the two variants of a text with TA(), which addresses the reader.
/// </summary>
/// <remarks>
/// Many languages tell a formal address from an informal one, such as "Sie" and "du" in German. A text with TA()
/// therefore has two keys: its usual key, whose translation addresses the reader formally like all texts of the
/// app, and the same key with INFORMAL_SUFFIX, whose translation addresses the reader informally. The I18N
/// collector writes both keys, and the app looks up the informal one when the person wants to be addressed so.
/// </remarks>
public static class I18NAddressVariant
{
    /// <summary>
    /// The end of the key of the variant with the informal address.
    /// </summary>
    public const string INFORMAL_SUFFIX = "::INFORMAL";

    /// <summary>
    /// Gets the key of the variant with the informal address.
    /// </summary>
    /// <param name="key">The usual key of the text, which stands for the formal address.</param>
    /// <returns>The key of the informal variant.</returns>
    public static string InformalKeyOf(string key) => $"{key}{INFORMAL_SUFFIX}";

    /// <summary>
    /// Tries to get the usual key of a text from the key of its informal variant.
    /// </summary>
    /// <param name="key">Any key.</param>
    /// <param name="formalKey">The usual key of the text, or the given key when it belongs to no informal variant.</param>
    /// <returns>True, when the given key belongs to an informal variant.</returns>
    public static bool TryGetFormalKey(string key, out string formalKey)
    {
        if (key.EndsWith(INFORMAL_SUFFIX, StringComparison.Ordinal))
        {
            formalKey = key[..^INFORMAL_SUFFIX.Length];
            return true;
        }

        formalKey = key;
        return false;
    }
}