using System.Buffers;
using System.Globalization;
using System.Text;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Validation;

/// <summary>
/// Checks whether an API key can be sent to a provider at all.
/// </summary>
/// <remarks>
/// <para>
/// API keys travel in HTTP headers, and these carry nothing but ASCII. .NET refuses every other
/// character only while sending, with a message nobody understands, such as "Request headers must
/// contain only ASCII characters." Two of its messages about line breaks even quote the value of
/// the header, i.e., the key. Hence, AI Studio checks what a key picked up on its way through the
/// clipboard before storing it, and again before sending it.
/// </para>
/// <para>
/// Whitespace around the key goes: nobody means it, and HTTP drops it anyway. Everything else is
/// reported instead of removed. A key with an invisible character in its middle might as well be
/// two keys glued together, and guessing which part is the key would send the wrong one.
/// </para>
/// <para>
/// Nothing here writes the key anywhere. Messages name at most the one character which cannot be
/// sent, and only when the user can see it; logs name the kind of the issue alone.
/// </para>
/// </remarks>
public static class APIKeyValidation
{
    private static string TA(string fallbackEN) => I18N.I.TA(fallbackEN, typeof(APIKeyValidation).Namespace, nameof(APIKeyValidation));

    /// <summary>
    /// A blank pattern of the Braille block: a letter by its category, but nothing to see.
    /// </summary>
    private const int BRAILLE_PATTERN_BLANK = 0x2800;

    /// <summary>
    /// Removes the whitespace around a key, line breaks and no-break spaces included.
    /// </summary>
    /// <remarks>
    /// Zero-width spaces and byte order marks stay: .NET does not count them as whitespace, and
    /// the check reports them.
    /// </remarks>
    /// <param name="key">The key as the user entered it.</param>
    /// <returns>The key without the whitespace around it, or an empty string for no key.</returns>
    public static string Normalize(string? key) => key?.Trim() ?? string.Empty;

    /// <summary>
    /// Finds the first character of a key which cannot be sent.
    /// </summary>
    /// <remarks>
    /// The key is read character by character as the user sees them, i.e., by grapheme cluster:
    /// an umlaut typed as a letter and a combining mark, as macOS does in file names, is one
    /// character and is reported as such, not as an invisible mark. An empty key is no issue,
    /// since whether a key may be missing is up to the caller.
    /// </remarks>
    /// <param name="key">The key as the user entered it.</param>
    /// <returns>The first issue, or NONE when the key can be sent.</returns>
    public static APIKeyIssue Check(string? key) => Check(key, allowsSpaces: false);

    /// <summary>
    /// Finds the first character of a value for an HTTP header which cannot be sent.
    /// </summary>
    /// <remarks>
    /// The same check as for an API key, for values which people choose themselves, such as the
    /// password of an ERI server: there, a space inside the value is fine and goes into the header
    /// as it is. Every other whitespace inside the value is still an issue, the no-break space included.
    /// </remarks>
    /// <param name="value">The value as the user entered it.</param>
    /// <param name="allowsSpaces">Whether a plain space inside the value is fine.</param>
    /// <returns>The first issue, or NONE when the value can be sent.</returns>
    public static APIKeyIssue Check(string? value, bool allowsSpaces)
    {
        var characters = StringInfo.GetTextElementEnumerator(Normalize(value));
        while (characters.MoveNext())
        {
            var issue = CheckCharacter(characters.GetTextElement(), allowsSpaces);
            if (issue.IsProblem)
                return issue;
        }

        return APIKeyIssue.NONE;
    }

    /// <summary>
    /// Prepares a key for an HTTP header.
    /// </summary>
    /// <param name="key">The key as it was entered or stored.</param>
    /// <param name="headerValue">The key without the whitespace around it, or an empty string when it cannot be sent.</param>
    /// <returns>True when the key can be sent, which includes an empty key.</returns>
    public static bool TryPrepareForHeader(string? key, out string headerValue)
    {
        headerValue = Normalize(key);
        if (!Check(headerValue).IsProblem)
            return true;

        headerValue = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks a key the user is entering.
    /// </summary>
    /// <param name="key">The key as the user entered it.</param>
    /// <returns>What is wrong with the key, or null when it can be sent.</returns>
    public static string? Validate(string? key)
    {
        var issue = Check(key);
        return issue.IsProblem ? GetMessage(issue) : null;
    }

    /// <summary>
    /// Tells the user what is wrong with the key they entered.
    /// </summary>
    /// <param name="issue">The issue the check found.</param>
    /// <returns>The message, or an empty string when there is no issue.</returns>
    public static string GetMessage(APIKeyIssue issue) => issue.Kind switch
    {
        APIKeyIssueKind.WHITESPACE => TA("The API key contains a space or a line break, which an API key cannot contain. Please check whether you copied more than the key."),
        APIKeyIssueKind.INVISIBLE_CHARACTER => TA("The API key contains an invisible character, which often comes along when you copy text from a web page or a document. Please delete the key and enter it again."),
        APIKeyIssueKind.NON_ASCII_CHARACTER => string.Format(TA("The API key contains the character '{0}', which an API key cannot contain. Word processors, for example, turn quotation marks and dashes into such characters. Please check the key."), issue.Character),

        _ => string.Empty,
    };

    /// <summary>
    /// Tells the user that a stored key cannot be sent.
    /// </summary>
    /// <remarks>
    /// Meant for a key which was stored before AI Studio checked keys, or which reached the keyring
    /// some other way. The user may not be able to change it, e.g., when the organization provides
    /// it, so the message names that way out as well.
    /// </remarks>
    /// <param name="ownerName">The provider or service the key belongs to.</param>
    /// <returns>The message.</returns>
    public static string GetUnusableKeyMessage(string ownerName) => string.Format(TA("The API key for '{0}' contains a character which an API key cannot contain, such as a line break or an invisible character. Please enter the key again in the settings. If your organization provides the key, please contact your IT department."), ownerName);

    private static APIKeyIssue CheckCharacter(string character, bool allowsSpaces)
    {
        if (character.Length is 1 && (character[0] is >= '!' and <= '~' || (allowsSpaces && character[0] is ' ')))
            return APIKeyIssue.NONE;

        //
        // A character as the user sees it may consist of several code points, e.g., a letter and
        // its accent, or a letter and a zero-width joiner someone hid behind it. Whitespace wins,
        // since it tells the user best what went wrong while copying. Anything invisible in the
        // character makes the whole character one nobody can point at:
        //
        var isInvisible = false;
        var remaining = character.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var codePoint, out var length) is not OperationStatus.Done)
            {
                // A lone surrogate is no character at all:
                isInvisible = true;
                remaining = remaining[Math.Max(length, 1)..];
                continue;
            }

            if (Rune.IsWhiteSpace(codePoint))
                return new(APIKeyIssueKind.WHITESPACE, string.Empty);

            if (IsInvisible(codePoint))
                isInvisible = true;

            remaining = remaining[length..];
        }

        return isInvisible
            ? new(APIKeyIssueKind.INVISIBLE_CHARACTER, string.Empty)
            : new(APIKeyIssueKind.NON_ASCII_CHARACTER, character.Normalize(NormalizationForm.FormC));
    }

    /// <summary>
    /// Whether a code point shows nothing by itself.
    /// </summary>
    /// <remarks>
    /// Besides control and format characters, these are the default-ignorable code points of
    /// Unicode, which a renderer shows as nothing even where their category says otherwise, e.g.,
    /// the Hangul fillers, which count as letters, and the variation selectors, which count as marks.
    /// </remarks>
    private static bool IsInvisible(Rune codePoint) => Rune.GetUnicodeCategory(codePoint) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate
        || codePoint.Value is
            0x00AD or 0x034F or 0x061C or 0x3164 or 0xFEFF or 0xFFA0 or BRAILLE_PATTERN_BLANK or
            (>= 0x115F and <= 0x1160) or
            (>= 0x17B4 and <= 0x17B5) or
            (>= 0x180B and <= 0x180F) or
            (>= 0x200B and <= 0x200F) or
            (>= 0x202A and <= 0x202E) or
            (>= 0x2060 and <= 0x206F) or
            (>= 0xFE00 and <= 0xFE0F) or
            (>= 0xFFF0 and <= 0xFFF8) or
            (>= 0x1BCA0 and <= 0x1BCA3) or
            (>= 0x1D173 and <= 0x1D17A) or
            (>= 0xE0000 and <= 0xE0FFF);
}