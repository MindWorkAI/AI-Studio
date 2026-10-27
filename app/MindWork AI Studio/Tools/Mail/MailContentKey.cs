using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Gives a mail the key of its document in the index, the same in every folder it lies in.
/// </summary>
/// <remarks>
/// The key follows the mail rather than its place: a mail moved to another folder, or found again
/// after the server reset the UIDs of a folder (UIDVALIDITY), keeps its key, so only its location
/// changes and nothing gets embedded again. In the order the server can tell, it comes from:
/// <list type="number">
///   <item>the EMAILID of RFC 8474, which the server keeps for a mail across all its folders;</item>
///   <item>the X-GM-MSGID, which is the same for Gmail;</item>
///   <item>a hash over Message-ID, Date, From, Subject and the size, all of which a server reports
///   without the mail itself being fetched. Two mails alike in all of these are copies of one mail.</item>
/// </list>
/// The key is "mail:" and a SHA-256 in hex, whichever of the three it comes from. The value it
/// hashes names its origin, so an EMAILID which happens to read like a Gmail id cannot meet one.
/// The key goes through no Path API, see "Indexed data sources" in AGENTS.md.
/// </remarks>
public static class MailContentKey
{
    public const string PREFIX = "mail:";

    /// <summary>
    /// Builds the key of a mail.
    /// </summary>
    /// <param name="identity">What the server reported about the mail.</param>
    /// <returns>The key, starting with "mail:".</returns>
    public static string Create(MailIdentity identity)
    {
        if (!string.IsNullOrWhiteSpace(identity.EmailId))
            return Hash($"emailid\n{identity.EmailId.Trim()}");

        if (identity.GmailMessageId is { } gmailMessageId)
            return Hash($"x-gm-msgid\n{gmailMessageId.ToString(CultureInfo.InvariantCulture)}");

        var fields = new StringBuilder("headers");
        fields.Append('\n').Append(NormalizeMessageId(identity.MessageId));
        fields.Append('\n').Append(identity.Date?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        fields.Append('\n').Append(string.Join(',', identity.FromAddresses.Select(address => address.Trim().ToLowerInvariant())));
        fields.Append('\n').Append(NormalizeWhitespace(identity.Subject));
        fields.Append('\n').Append(identity.Size.ToString(CultureInfo.InvariantCulture));
        return Hash(fields.ToString());
    }

    private static string Hash(string value) => PREFIX + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// The Message-ID without its angle brackets, which one reader keeps and the next one drops.
    /// </summary>
    private static string NormalizeMessageId(string? messageId)
    {
        var trimmed = messageId?.Trim() ?? string.Empty;
        return trimmed.Length > 1 && trimmed[0] == '<' && trimmed[^1] == '>' ? trimmed[1..^1].Trim() : trimmed;
    }

    /// <summary>
    /// A subject with every run of whitespace as one space, since folding a long header line
    /// leaves line breaks behind which not every reader unfolds alike.
    /// </summary>
    private static string NormalizeWhitespace(string? text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}