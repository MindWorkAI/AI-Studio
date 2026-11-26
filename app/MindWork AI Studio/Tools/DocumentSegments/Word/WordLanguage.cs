using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Notes the language of new words in a Word document, so that its spell checking and hyphenation follow it.
/// </summary>
internal static class WordLanguage
{
    /// <summary>
    /// The languages Word records apart from the others: those with East Asian characters, and those with complex scripts.
    /// </summary>
    private static readonly string[] EAST_ASIAN_LANGUAGES = ["zh", "ja", "ko"];
    private static readonly string[] COMPLEX_SCRIPT_LANGUAGES = ["ar", "fa", "he", "hi", "ur"];

    /// <summary>
    /// Notes the language of a run.
    /// </summary>
    /// <param name="run">The run with new words.</param>
    /// <param name="languageTag">The IETF tag of the language, e.g., "de-DE"; empty to leave the run as it is.</param>
    public static void Set(W.Run run, string languageTag)
    {
        if (languageTag.Length == 0)
            return;

        var properties = run.RunProperties ?? run.PrependChild(new W.RunProperties());
        var languages = properties.Languages ??= new W.Languages();
        var primaryLanguage = languageTag.Split('-')[0];
        if (EAST_ASIAN_LANGUAGES.Contains(primaryLanguage, StringComparer.OrdinalIgnoreCase))
            languages.EastAsia = languageTag;
        else if (COMPLEX_SCRIPT_LANGUAGES.Contains(primaryLanguage, StringComparer.OrdinalIgnoreCase))
            languages.Bidi = languageTag;
        else
            languages.Val = languageTag;
    }
}