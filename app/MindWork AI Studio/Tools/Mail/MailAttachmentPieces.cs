using MimeKit;
using MimeKit.IO;
using MimeKit.IO.Filters;
using MimeKit.Utils;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Writes out an attachment as the file it was, from the pieces a server delivers it in.
/// </summary>
/// <remarks>
/// An IMAP client holds every answer of the server in memory as a whole, so an attachment fetched
/// at once would take as much memory as it is large. Fetched piece by piece, with each piece decoded
/// and written out at once, an attachment takes no more memory than one piece, however large it is.
/// This keeps AI Studio usable on machines with little memory, too.
/// </remarks>
public static class MailAttachmentPieces
{
    /// <summary>
    /// How large a piece is, as it travels.
    /// </summary>
    public const int PIECE_BYTES = 4 * 1024 * 1024;

    /// <summary>
    /// The largest attachment, as it travels, which can be fetched at all.
    /// </summary>
    /// <remarks>
    /// The IMAP client addresses a piece by an offset of type int. With Base64, this allows for files
    /// of about 1.5 GB, whereas mail servers rarely accept a mail a tenth as large.
    /// </remarks>
    public const long MAX_OCTETS = int.MaxValue;

    private const int COPY_BUFFER_BYTES = 81_920;

    /// <summary>
    /// Fetches an attachment piece by piece and writes it out decoded.
    /// </summary>
    /// <param name="fetchPieceAsync">Fetches the piece which starts at the given offset and is at most pieceBytes long. A shorter piece is the last one, and a piece beyond the end is empty.</param>
    /// <param name="transferEncoding">The transfer encoding of the attachment, e.g. "base64", or null when the mail names none.</param>
    /// <param name="destination">Where the decoded attachment goes. It is left open.</param>
    /// <param name="pieceBytes">How large a piece is, cf. PIECE_BYTES.</param>
    /// <param name="token">The cancellation token.</param>
    /// <exception cref="InvalidDataException">The transfer encoding is unknown, or the server delivered more than it was asked for.</exception>
    public static async Task WriteDecodedAsync(Func<int, CancellationToken, Task<Stream>> fetchPieceAsync, string? transferEncoding, Stream destination, int pieceBytes, CancellationToken token)
    {
        var encoding = GetEncoding(transferEncoding);
        await using var decoded = new FilteredStream(destination);
        if (encoding is ContentEncoding.Base64 or ContentEncoding.QuotedPrintable or ContentEncoding.UUEncode)
            decoded.Add(DecoderFilter.Create(encoding));

        var buffer = new byte[COPY_BUFFER_BYTES];
        for (var offset = 0L; offset <= MAX_OCTETS; offset += pieceBytes)
        {
            var pieceLength = 0L;
            await using (var piece = await fetchPieceAsync((int)offset, token))
            {
                int read;
                while ((read = await piece.ReadAsync(buffer, token)) > 0)
                {
                    pieceLength += read;
                    if (pieceLength > pieceBytes)
                        throw new InvalidDataException("The server delivered a piece of an attachment larger than asked for.");

                    await decoded.WriteAsync(buffer.AsMemory(0, read), token);
                }
            }

            // A piece shorter than asked for is the last one:
            if (pieceLength < pieceBytes)
            {
                await decoded.FlushAsync(token);
                return;
            }
        }

        throw new InvalidDataException("The attachment is larger than any attachment which can be fetched.");
    }

    private static ContentEncoding GetEncoding(string? transferEncoding)
    {
        // Without a transfer encoding, the content travels as it is, cf. RFC 2045, section 6.1:
        if (string.IsNullOrWhiteSpace(transferEncoding))
            return ContentEncoding.Default;

        if (MimeUtils.TryParse(transferEncoding, out ContentEncoding encoding))
            return encoding;

        throw new InvalidDataException("The attachment has a transfer encoding unknown to AI Studio.");
    }
}