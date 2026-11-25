using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.Mail;

/// <summary>
/// The encoding in which AI Studio sends the username and the password of a mailbox.
/// </summary>
/// <remarks>
/// The standard for signing in with a password demands UTF-8 (RFC 4616), but a mail server compares
/// the bytes it receives with what it stored, and depending on its configuration, it may decode them
/// in another encoding. Microsoft Exchange, e.g., may expect the code page of its Windows system:
/// Windows-1252 in Western Europe, Windows-1254 in Turkey, or Shift-JIS in Japan. ASCII comes out the
/// same in all of them, so only users whose username or password holds other characters notice.<br/><br/>
/// AI Studio therefore offers every encoding .NET knows which keeps ASCII as it is, stored by its name,
/// e.g., "windows-1254". UTF-16, UTF-32, UTF-7, and the EBCDIC code pages are left out: they change
/// ASCII, and SASL PLAIN separates its parts with zero bytes, which UTF-16 and UTF-32 would break.<br/><br/>
/// A name this version does not know, e.g., one written into the settings by hand, keeps its name, so
/// that it is stored as it was, but signs in nowhere, cf. IsKnown. Falling back to UTF-8 instead would
/// sign in with bytes which may not be the password, and every such attempt counts as a failed sign-in,
/// which can lock the account. Two values are equal when their names are, cf. Name.
/// </remarks>
[JsonConverter(typeof(MailboxPasswordEncodingJsonConverter))]
public readonly record struct MailboxPasswordEncoding
{
    /// <summary>
    /// The characters of ASCII, which every encoding a mailbox may use has to keep as they are.
    /// </summary>
    private static readonly string ASCII_TEXT = new(Enumerable.Range(0, 128).Select(code => (char)code).ToArray());

    /// <summary>
    /// The bytes of ASCII_TEXT in ASCII.
    /// </summary>
    private static readonly byte[] ASCII_BYTES = Encoding.ASCII.GetBytes(ASCII_TEXT);

    /// <summary>
    /// The encoding of a new mailbox, and of one stored before the setting existed: UTF-8, which AI Studio always used until then.
    /// </summary>
    public static readonly MailboxPasswordEncoding DEFAULT;

    /// <summary>
    /// All encodings a mailbox may use: UTF-8 first, the others in alphabetical order of their names.
    /// </summary>
    public static readonly IReadOnlyList<MailboxPasswordEncoding> ALL;

    static MailboxPasswordEncoding()
    {
        // .NET knows the code pages of Windows and most others only once their provider is registered:
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        DEFAULT = FromName("utf-8");
        ALL = Encoding.GetEncodings()
            .Select(info => FromName(info.Name))
            .Where(encoding => encoding.IsKnown)
            .Distinct()
            .OrderBy(encoding => encoding != DEFAULT)
            .ThenBy(encoding => encoding.Name, StringComparer.Ordinal)
            .ToList();
    }

    private MailboxPasswordEncoding(string name, Encoding? textEncoding)
    {
        this.Name = name;
        this.TextEncoding = textEncoding;
    }

    /// <summary>
    /// The name of the encoding: the one .NET gives it, e.g., "windows-1252", or, for an encoding this
    /// version does not know, the name as it was written.
    /// </summary>
    public string Name => field ?? string.Empty;

    /// <summary>
    /// The encoding to hand to the IMAP client. It refuses what it cannot encode instead of replacing
    /// it, e.g., with a question mark: a password sent with a replaced character is not the password.
    /// </summary>
    /// <value>The encoding, or null when this version does not know it.</value>
    public Encoding? TextEncoding { get; }

    /// <summary>
    /// Whether this version knows the encoding, and so whether a mailbox may sign in with it.
    /// </summary>
    public bool IsKnown => this.TextEncoding is not null;

    /// <summary>
    /// Whether the encoding is UTF-8, the only one the LOGIN command of the IMAP client sends.
    /// </summary>
    public bool IsUtf8 => this.TextEncoding?.CodePage == Encoding.UTF8.CodePage;

    /// <summary>
    /// Finds the encoding of a name.
    /// </summary>
    /// <param name="name">The name of the encoding, e.g., "windows-1252", in any case and with spaces around it.</param>
    /// <returns>The encoding. When .NET does not know the name, or when the encoding changes ASCII, one which signs in nowhere, cf. IsKnown.</returns>
    public static MailboxPasswordEncoding FromName(string? name)
    {
        var unknown = new MailboxPasswordEncoding(name ?? string.Empty, null);
        if (string.IsNullOrWhiteSpace(name))
            return unknown;

        try
        {
            var candidate = Encoding.GetEncoding(name.Trim(), EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            return candidate.GetBytes(ASCII_TEXT).AsSpan().SequenceEqual(ASCII_BYTES) ? new(candidate.WebName, candidate) : unknown;
        }

        // Thrown for a name .NET does not know, and for an encoding which lacks a character of ASCII.
        // EncoderFallbackException is one of them:
        catch (ArgumentException)
        {
            return unknown;
        }

        // Thrown for an encoding .NET knows but no longer supports, such as UTF-7:
        catch (NotSupportedException)
        {
            return unknown;
        }
    }

    /// <summary>
    /// Whether the bytes of a username and a password depend on the encoding: only characters beyond ASCII do.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>True when either holds a character beyond ASCII.</returns>
    public static bool DependsOnEncoding(string username, string password) => !Ascii.IsValid(username) || !Ascii.IsValid(password);

    /// <summary>
    /// The encoding to offer for one more connection test after the server refused a username or a
    /// password with characters beyond ASCII.
    /// </summary>
    /// <remarks>
    /// A server which expects another encoding than UTF-8 mostly expects the code page of the Windows
    /// system it runs on, and that is mostly the code page of the region its users live in: the ANSI
    /// code page of Windows for the language of the operating system, e.g., windows-1252 for de-DE or
    /// windows-1254 for tr-TR. When the test used that one already, UTF-8 comes next. An encoding which
    /// lacks a character of the username or the password is never offered. It is only an offer: the
    /// user decides, and every test is one attempt to sign in.
    /// </remarks>
    /// <param name="localeTag">The language of the operating system, e.g., "de-DE".</param>
    /// <param name="testedEncoding">The encoding the refused test used.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The encoding, or null when there is none to offer.</returns>
    public static MailboxPasswordEncoding? Suggest(string localeTag, MailboxPasswordEncoding testedEncoding, string username, string password)
    {
        if (!DependsOnEncoding(username, password))
            return null;

        foreach (var candidate in new[] { GetRegionalEncoding(localeTag), DEFAULT })
        {
            if (candidate is not { } encoding || encoding == testedEncoding)
                continue;

            if (encoding.TryPrepare(username, out _) && encoding.TryPrepare(password, out _))
                return encoding;
        }

        return null;
    }

    /// <summary>
    /// Prepares a username or a password for this encoding.
    /// </summary>
    /// <remarks>
    /// For every encoding but UTF-8, the text is normalized first (NFC): an ä may arrive as an a
    /// followed by a combining diaeresis, e.g., from a password manager, and the code pages mostly have
    /// only the composed ä. UTF-8 takes the text as it is, as AI Studio always sent it.
    /// </remarks>
    /// <param name="text">The username or the password.</param>
    /// <param name="prepared">The text to sign in with.</param>
    /// <returns>Whether this version knows the encoding and it has every character of the text.</returns>
    public bool TryPrepare(string text, out string prepared)
    {
        prepared = text;
        if (this.TextEncoding is not { } textEncoding)
            return false;

        try
        {
            if (!this.IsUtf8)
                prepared = text.Normalize(NormalizationForm.FormC);

            textEncoding.GetByteCount(prepared);
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

    /// <inheritdoc />
    public bool Equals(MailboxPasswordEncoding other) => string.Equals(this.Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Name);

    /// <summary>
    /// The name of the encoding, as the mailbox dialog shows it, cf. Name.
    /// </summary>
    public override string ToString() => this.Name;

    /// <summary>
    /// The ANSI code page of Windows for a language, cf. Suggest.
    /// </summary>
    /// <param name="localeTag">The language, e.g., "tr-TR".</param>
    /// <returns>The encoding, or null for an unknown language and for one without a code page of its own, such as Hindi.</returns>
    private static MailboxPasswordEncoding? GetRegionalEncoding(string localeTag)
    {
        if (string.IsNullOrWhiteSpace(localeTag))
            return null;

        try
        {
            var codePage = CultureInfo.GetCultureInfo(localeTag.Trim()).TextInfo.ANSICodePage;
            return codePage is 0 ? null : FromName(Encoding.GetEncoding(codePage).WebName);
        }

        // Thrown for a language .NET does not know, and for a code page it does not support.
        // CultureNotFoundException is one of them:
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}