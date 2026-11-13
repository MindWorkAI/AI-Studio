using MimeKit;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Reads how important the sender marked a mail.
/// </summary>
/// <remarks>
/// Three headers say so, from three different eras, and mail programs send whichever they like,
/// often more than one. They are read in this order, and the first one with a value AI Studio
/// understands decides:
/// <list type="number">
///   <item>Importance (RFC 2156): low, normal or high. Outlook sends it.</item>
///   <item>X-Priority: a number from 1 (highest) to 5 (lowest), often followed by a word such as "1 (Highest)".</item>
///   <item>Priority (RFC 2156): non-urgent, normal or urgent.</item>
/// </list>
/// A header with a value nobody defined is skipped rather than read as normal, so that the next
/// header still gets its say.
/// </remarks>
public static class MailImportanceDetection
{
    /// <summary>
    /// Reads the importance from the header block of a mail.
    /// </summary>
    /// <param name="headers">The header block.</param>
    /// <returns>The importance, NORMAL when no header says otherwise.</returns>
    public static MailImportance Detect(HeaderList headers) =>
        ReadImportance(headers[HeaderId.Importance]) ??
        ReadXPriority(headers[HeaderId.XPriority]) ??
        ReadPriority(headers[HeaderId.Priority]) ??
        MailImportance.NORMAL;

    private static MailImportance? ReadImportance(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => MailImportance.LOW,
        "normal" => MailImportance.NORMAL,
        "high" => MailImportance.HIGH,
        _ => null,
    };

    private static MailImportance? ReadXPriority(string? value)
    {
        // Only the leading digit counts, as in "2 (High)", and it has to stand alone:
        var trimmed = value?.TrimStart();
        if (string.IsNullOrEmpty(trimmed) || (trimmed.Length > 1 && char.IsDigit(trimmed[1])))
            return null;

        return trimmed[0] switch
        {
            '1' or '2' => MailImportance.HIGH,
            '3' => MailImportance.NORMAL,
            '4' or '5' => MailImportance.LOW,
            _ => null,
        };
    }

    private static MailImportance? ReadPriority(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "non-urgent" => MailImportance.LOW,
        "normal" => MailImportance.NORMAL,
        "urgent" => MailImportance.HIGH,
        _ => null,
    };
}