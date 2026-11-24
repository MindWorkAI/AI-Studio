using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace AIStudio.Tools.Mail;

/// <summary>
/// The encodings in which AI Studio can send the username and the password of a mailbox.
/// </summary>
/// <remarks>
/// The standard for signing in with a password demands UTF-8 (RFC 4616), but a mail server compares
/// the bytes it receives with what it stored, and depending on its configuration, it may decode them
/// in another encoding. Microsoft Exchange, e.g., may expect the code page of its Windows system:
/// Windows-1252 in Western Europe, Windows-1254 in Turkey, or Shift-JIS in Japan. ASCII comes out the
/// same in all of them, so only users whose username or password holds other characters notice.<br/><br/>
/// AI Studio therefore offers every encoding .NET knows which keeps ASCII as it is, stored by its name,
/// e.g., "windows-1254". UTF-16, UTF-32, UTF-7, and the EBCDIC code pages are left out: they change
/// ASCII, and SASL PLAIN separates its parts with zero bytes, which UTF-16 and UTF-32 would break.
/// </remarks>
public static class MailboxPasswordEncodings
{
    /// <summary>
    /// The encoding of a new mailbox, and of one stored before the setting existed: UTF-8, which AI Studio always used until then.
    /// </summary>
    public const string DEFAULT = "utf-8";

    /// <summary>
    /// The characters of ASCII, which every encoding a mailbox may use has to keep as they are.
    /// </summary>
    private static readonly string ASCII_TEXT = new(Enumerable.Range(0, 128).Select(code => (char)code).ToArray());

    /// <summary>
    /// The bytes of ASCII_TEXT in ASCII.
    /// </summary>
    private static readonly byte[] ASCII_BYTES = Encoding.ASCII.GetBytes(ASCII_TEXT);

    /// <summary>
    /// The names of all encodings a mailbox may use: UTF-8 first, the others in alphabetical order.
    /// </summary>
    public static readonly IReadOnlyList<string> ALL;

    static MailboxPasswordEncodings()
    {
        // .NET knows the code pages of Windows and most others only once their provider is registered:
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ALL = Encoding.GetEncodings()
            .Select(info => TryGet(info.Name, out var encoding) ? encoding.WebName : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => !string.Equals(name, DEFAULT, StringComparison.OrdinalIgnoreCase))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Finds an encoding a mailbox may use.
    /// </summary>
    /// <remarks>
    /// The encoding refuses what it cannot encode instead of replacing it, e.g., with a question mark:
    /// a password sent with a replaced character is not the password, and every such attempt counts as
    /// a failed sign-in, which can lock the account.
    /// </remarks>
    /// <param name="name">The name of the encoding, e.g., "windows-1252", in any case and with spaces around it.</param>
    /// <param name="encoding">The encoding, which throws on a character it lacks.</param>
    /// <returns>Whether .NET knows the encoding and it keeps ASCII as it is.</returns>
    public static bool TryGet(string? name, [NotNullWhen(true)] out Encoding? encoding)
    {
        encoding = null;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        try
        {
            var candidate = Encoding.GetEncoding(name.Trim(), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            if (!candidate.GetBytes(ASCII_TEXT).AsSpan().SequenceEqual(ASCII_BYTES))
                return false;

            encoding = candidate;
            return true;
        }

        // Thrown for a name .NET does not know, and for an encoding which lacks a character of ASCII.
        // EncoderFallbackException is one of them:
        catch (ArgumentException)
        {
            return false;
        }

        // Thrown for an encoding .NET knows but no longer supports, such as UTF-7:
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether an encoding is UTF-8, the only one the LOGIN command of the IMAP client sends.
    /// </summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns>True for UTF-8.</returns>
    public static bool IsUtf8(Encoding encoding) => encoding.CodePage == Encoding.UTF8.CodePage;

    /// <summary>
    /// Prepares a username or a password for an encoding.
    /// </summary>
    /// <remarks>
    /// For every encoding but UTF-8, the text is normalized first (NFC): an ä may arrive as an a
    /// followed by a combining diaeresis, e.g., from a password manager, and the code pages mostly have
    /// only the composed ä. UTF-8 takes the text as it is, as AI Studio always sent it.
    /// </remarks>
    /// <param name="encoding">The encoding, as TryGet returns it.</param>
    /// <param name="text">The username or the password.</param>
    /// <param name="prepared">The text to sign in with.</param>
    /// <returns>Whether the encoding has every character of the text.</returns>
    public static bool TryPrepare(Encoding encoding, string text, out string prepared)
    {
        prepared = text;
        try
        {
            if (!IsUtf8(encoding))
                prepared = text.Normalize(NormalizationForm.FormC);

            encoding.GetByteCount(prepared);
            return true;
        }

        // Thrown for a character the encoding lacks, and for a lone surrogate, which no encoding has.
        // EncoderFallbackException is one of them:
        catch (ArgumentException)
        {
            prepared = text;
            return false;
        }
    }
}