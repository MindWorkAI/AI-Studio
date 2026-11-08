using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The notes which the BeyondFacts Lab shows before the first use, and again whenever their text changes.
/// </summary>
/// <remarks>
/// The settings keep a hash of the notes the person confirmed. A hash over both languages lets a change of either
/// show the notes again, also to people who read the other language, since its content changed as well.
/// </remarks>
public static class BflNotice
{
    /// <summary>
    /// The notes in German.
    /// </summary>
    public const string GERMAN_PATH = "Assistants/BeyondFactsLab/Notice/notice.de.md";

    /// <summary>
    /// The notes in English, for every other language.
    /// </summary>
    public const string ENGLISH_PATH = "Assistants/BeyondFactsLab/Notice/notice.en.md";

    /// <summary>
    /// Reads the notes in the language of the app.
    /// </summary>
    /// <param name="ietfTag">The IETF tag of the language of the app, e.g., "de-DE".</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The notes as Markdown, or an empty string when they are missing.</returns>
    public static async Task<string> ReadAsync(string ietfTag, CancellationToken token = default) => await AppResources.ReadTextAsync(PathFor(ietfTag), token);

    /// <summary>
    /// Computes the hash of the notes as they come with this version.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    public static async Task<string> ComputeHashAsync(CancellationToken token = default) =>
        ComputeHash(await AppResources.ReadTextAsync(GERMAN_PATH, token), await AppResources.ReadTextAsync(ENGLISH_PATH, token));

    /// <summary>
    /// Gets the file of the notes in a language.
    /// </summary>
    /// <param name="ietfTag">The IETF tag of the language, e.g., "de-DE".</param>
    public static string PathFor(string ietfTag) => ietfTag.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? GERMAN_PATH : ENGLISH_PATH;

    /// <summary>
    /// Computes the hash of the notes in both languages.
    /// </summary>
    /// <remarks>
    /// Line endings and the space around the text do not count, so that a checkout with other line endings does not
    /// show the notes again.
    /// </remarks>
    /// <param name="german">The notes in German.</param>
    /// <param name="english">The notes in English.</param>
    /// <returns>The hash, as lowercase hexadecimal digits.</returns>
    public static string ComputeHash(string german, string english)
    {
        var text = $"{Normalize(german)}\0{Normalize(english)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// Gets whether the person confirmed the notes as they are now.
    /// </summary>
    /// <param name="acceptedHash">The hash the settings keep.</param>
    /// <param name="currentHash">The hash of the notes as they come with this version.</param>
    public static bool IsAccepted(string acceptedHash, string currentHash) => string.Equals(acceptedHash, currentHash, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();
}