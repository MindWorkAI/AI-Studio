using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The notes which the BeyondFacts Lab shows before the first use, and again whenever their text changes.
/// </summary>
/// <remarks>
/// The notes are a text of the localization, so they come in every language of the app. The settings keep a hash of
/// the notes as the app showed them when the person confirmed them. A new wording, in English or in a translation,
/// therefore shows them again, and so does a switch to another language or to another form of address, whose notes
/// the person has not read yet.
/// </remarks>
public static class BflNotice
{
    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflNotice).Namespace, nameof(BflNotice));

    /// <summary>
    /// Gets the notes in the language of the app and in the form of address the person chose, as Markdown.
    /// </summary>
    public static string Text => TA("""
        ## Welcome!

        The BeyondFacts Lab guides you through an AI-supported process of reflection, which helps you take a position in challenging communication situations and stay able to communicate.

        Data protection depends on the AI provider you choose, its rules for processing and storing data, and your configuration. Enter only data that are suitable and approved for this.

        Please check all results critically. Depending on the AI model, the answers may differ. Hallucinations cannot be ruled out.

        This is a prototype, so we look forward to your feedback.
        """);

    /// <summary>
    /// Computes the hash of the notes as the app shows them.
    /// </summary>
    /// <remarks>
    /// Line endings and the space around the text do not count, so that a build from a checkout with other line endings
    /// does not show the notes again.
    /// </remarks>
    /// <param name="notes">The notes, as the app shows them.</param>
    /// <returns>The hash, as lowercase hexadecimal digits.</returns>
    public static string ComputeHash(string notes) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(notes.ReplaceLineEndings("\n").Trim())));

    /// <summary>
    /// Gets whether the person confirmed the notes as the app shows them now.
    /// </summary>
    /// <param name="acceptedHash">The hash the settings keep.</param>
    /// <param name="currentHash">The hash of the notes as the app shows them now.</param>
    public static bool IsAccepted(string acceptedHash, string currentHash) => string.Equals(acceptedHash, currentHash, StringComparison.OrdinalIgnoreCase);
}