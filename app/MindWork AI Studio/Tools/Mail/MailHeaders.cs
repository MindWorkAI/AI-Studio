using MimeKit;
using MimeKit.Utils;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Reads the header fields of a mail which carry addresses, Message-IDs and the date.
/// </summary>
/// <remarks>
/// One place for all readers of a header block, the text of a mail as well as what the index
/// keeps about it, so that both name the same people and the same date.
/// </remarks>
public static class MailHeaders
{
    /// <summary>
    /// Reads the addresses of all headers of one kind, e.g. both To lines of a mail which has two.
    /// </summary>
    /// <remarks>
    /// Parsed from the raw header rather than from its decoded text: a display name may carry an
    /// encoded comma, as in "=?utf-8?q?Doe=2C_John?=", which decoded first would split one person
    /// into two. The members of a group are read as addresses of their own.
    /// </remarks>
    /// <param name="headers">The header block.</param>
    /// <param name="headerId">The header, e.g. To.</param>
    /// <returns>The addresses in their order, empty when there is no such header or none can be read.</returns>
    public static IReadOnlyList<MailboxAddress> ReadMailboxes(HeaderList headers, HeaderId headerId)
    {
        var mailboxes = new List<MailboxAddress>();
        foreach (var header in headers.Where(header => header.Id == headerId))
        {
            if (InternetAddressList.TryParse(ParserOptions.Default, header.RawValue, out var list))
                mailboxes.AddRange(list.Mailboxes);
        }

        return mailboxes;
    }

    /// <summary>
    /// Reads the address of a mailbox, with an internationalized domain in Unicode, e.g. "müller.example"
    /// rather than "xn--mller-kva.example".
    /// </summary>
    /// <remarks>
    /// MimeKit decoded such a domain by itself up to version 4, and the index keeps every address in
    /// this form. The key of a mail depends on it, too, see MailContentKey: in another form, every mail
    /// from such a domain would be embedded anew.
    /// </remarks>
    /// <param name="mailbox">The mailbox, as parsed from a header.</param>
    /// <returns>The address.</returns>
    public static string ReadAddress(MailboxAddress mailbox) => mailbox.GetAddress(idnEncode: false);

    /// <summary>
    /// Reads the Message-IDs of a header, without their angle brackets.
    /// </summary>
    /// <param name="headers">The header block.</param>
    /// <param name="headerId">The header, e.g. References.</param>
    /// <returns>The Message-IDs in their order, empty when there is no such header.</returns>
    public static IReadOnlyList<string> ReadMessageIds(HeaderList headers, HeaderId headerId)
    {
        var value = headers[headerId];
        return string.IsNullOrWhiteSpace(value) ? [] : MimeUtils.EnumerateReferences(value).ToList();
    }

    /// <summary>
    /// Reads when the sender says the mail was written.
    /// </summary>
    /// <param name="headers">The header block.</param>
    /// <returns>The Date header, or null when there is none or it cannot be read.</returns>
    public static DateTimeOffset? ReadDate(HeaderList headers) => DateUtils.TryParse(headers[HeaderId.Date] ?? string.Empty, out var date) ? date : null;
}