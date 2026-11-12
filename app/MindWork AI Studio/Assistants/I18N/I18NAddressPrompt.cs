using SharedTools;

namespace AIStudio.Assistants.I18N;

/// <summary>
/// Tells the localization assistant how a text addresses the reader; see I18NAddressVariant.
/// </summary>
/// <remarks>
/// The usual key of a text gets the formal address, like every text of the app. Its informal variant starts from
/// the formal translation and changes only the form of address, so that both variants read alike, and a formal
/// translation which a person reviewed carries its wording over. The formal translations therefore come first.
/// </remarks>
public static class I18NAddressPrompt
{
    /// <summary>
    /// Puts the informal variants after all other texts and keeps the order otherwise.
    /// </summary>
    /// <param name="content">The texts to translate, by their keys.</param>
    /// <returns>The texts in the order to translate them.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> FormalFirst(IEnumerable<KeyValuePair<string, string>> content) => [.. content.OrderBy(entry => I18NAddressVariant.TryGetFormalKey(entry.Key, out _))];

    /// <summary>
    /// Finds the formal translation of a text whose informal variant comes next.
    /// </summary>
    /// <param name="informalKey">The key of the informal variant.</param>
    /// <param name="translatedNow">The texts translated in this run.</param>
    /// <param name="translatedBefore">The texts of the language plugin so far.</param>
    /// <returns>The formal translation, or null when there is none.</returns>
    public static string? FindFormalTranslation(string informalKey, IReadOnlyDictionary<string, string> translatedNow, IReadOnlyDictionary<string, string> translatedBefore)
    {
        if (!I18NAddressVariant.TryGetFormalKey(informalKey, out var formalKey))
            return null;

        if (translatedNow.TryGetValue(formalKey, out var translation) && !string.IsNullOrWhiteSpace(translation))
            return translation;

        if (translatedBefore.TryGetValue(formalKey, out translation) && !string.IsNullOrWhiteSpace(translation))
            return translation;

        return null;
    }

    /// <summary>
    /// Gets the section of the system prompt about the form of address.
    /// </summary>
    /// <param name="language">The target language.</param>
    /// <param name="isInformalVariant">Whether the text is the informal variant of a text.</param>
    /// <param name="formalTranslation">The formal translation of the text, when it is an informal variant and has one.</param>
    /// <returns>The section, as Markdown.</returns>
    public static string Section(string language, bool isInformalVariant, string? formalTranslation)
    {
        if (!isInformalVariant)
            return $"""
                    # Form of Address
                    When a text addresses the reader, use the formal address of {language}, such as "Sie" in
                    German, "vous" in French, or "usted" in Spanish.
                    """;

        if (string.IsNullOrWhiteSpace(formalTranslation))
            return $"""
                    # Form of Address
                    Address the reader informally, with the informal address of {language}, such as "du" in
                    German, "tu" in French, or "tú" in Spanish. When {language} has only one form of address,
                    translate as usual.
                    """;

        return $"""
                # Form of Address
                Address the reader informally, with the informal address of {language}, such as "du" in
                German, "tu" in French, or "tú" in Spanish. The formal translation of this text exists already:

                <formal_translation>
                {formalTranslation}
                </formal_translation>

                Keep its wording, its Markdown, and its line breaks, and change only what the informal address
                requires, such as pronouns, possessives, and verb forms. When {language} has only one form of
                address, answer with the formal translation unchanged.
                """;
    }
}