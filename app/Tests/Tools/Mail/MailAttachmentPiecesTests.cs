using System.Text;

using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks that an attachment fetched in pieces is written out as the file it was.
/// </summary>
/// <remarks>
/// The pieces are tiny here, so that they cut through the encoding in every possible place: inside
/// a group of Base64 characters, inside a line break, or inside an escape of quoted-printable.
/// </remarks>
[TestFixture]
public sealed class MailAttachmentPiecesTests
{
    private const int PIECE_BYTES = 7;

    [Test]
    public async Task Base64IsDecodedAcrossTheEndsOfThePieces()
    {
        var file = Enumerable.Range(0, 1000).Select(i => (byte)(i * 31 % 256)).ToArray();
        var encoded = Encoding.ASCII.GetBytes(Convert.ToBase64String(file, Base64FormattingOptions.InsertLineBreaks));

        Assert.That(await WriteDecodedAsync(new PieceServer(encoded), "base64"), Is.EqualTo(file));
    }

    [Test]
    public async Task QuotedPrintableIsDecodedAcrossTheEndsOfThePieces()
    {
        var encoded = Encoding.ASCII.GetBytes("a=3Db, and a rather l=\r\nong line about the caf=E9.\r\n");
        byte[] file = [..Encoding.ASCII.GetBytes("a=b, and a rather long line about the caf"), 0xE9, ..Encoding.ASCII.GetBytes(".\r\n")];

        Assert.That(await WriteDecodedAsync(new PieceServer(encoded), "quoted-printable"), Is.EqualTo(file));
    }

    [TestCase(null)]
    [TestCase("binary")]
    [TestCase("7bit")]
    public async Task ContentWithoutEncodingIsWrittenAsItIs(string? transferEncoding)
    {
        // Three full pieces, and then an empty one, which says that nothing follows:
        var file = Enumerable.Range(0, 3 * PIECE_BYTES).Select(i => (byte)i).ToArray();
        var server = new PieceServer(file);

        var written = await WriteDecodedAsync(server, transferEncoding);
        Assert.Multiple(() =>
        {
            Assert.That(written, Is.EqualTo(file));
            Assert.That(server.RequestedOffsets, Is.EqualTo(new[] { 0, 7, 14, 21 }));
        });
    }

    [Test]
    public async Task TheDestinationStaysOpen()
    {
        await using var destination = new MemoryStream();
        await MailAttachmentPieces.WriteDecodedAsync(new PieceServer([1, 2, 3]).FetchAsync, "binary", destination, PIECE_BYTES, CancellationToken.None);

        Assert.That(destination.CanWrite, Is.True, "The caller owns the file and closes it itself.");
    }

    [Test]
    public void AnUnknownEncodingFetchesNothing()
    {
        var server = new PieceServer(Encoding.ASCII.GetBytes("Uryyb"));
        Assert.Multiple(() =>
        {
            Assert.ThrowsAsync<InvalidDataException>(async () => await WriteDecodedAsync(server, "x-rot13"));
            Assert.That(server.RequestedOffsets, Is.Empty);
        });
    }

    [Test]
    public void APieceLargerThanAskedForIsRefused()
    {
        // Taken as it came, the next piece would repeat what this one already delivered:
        var server = new PieceServer(new byte[100], deliveredBytes: PIECE_BYTES + 1);
        Assert.ThrowsAsync<InvalidDataException>(async () => await WriteDecodedAsync(server, "binary"));
    }

    private static async Task<byte[]> WriteDecodedAsync(PieceServer server, string? transferEncoding)
    {
        await using var destination = new MemoryStream();
        await MailAttachmentPieces.WriteDecodedAsync(server.FetchAsync, transferEncoding, destination, PIECE_BYTES, CancellationToken.None);
        return destination.ToArray();
    }

    /// <summary>
    /// Delivers the pieces of an attachment, the way an IMAP server answers a partial fetch.
    /// </summary>
    /// <param name="content">The attachment, as it travels.</param>
    /// <param name="deliveredBytes">How many bytes it delivers per piece. A correct server delivers what it was asked for.</param>
    private sealed class PieceServer(byte[] content, int deliveredBytes = PIECE_BYTES)
    {
        public List<int> RequestedOffsets { get; } = [];

        public Task<Stream> FetchAsync(int offset, CancellationToken token)
        {
            this.RequestedOffsets.Add(offset);
            var start = Math.Min(offset, content.Length);
            var length = Math.Min(deliveredBytes, content.Length - start);
            return Task.FromResult<Stream>(new MemoryStream(content, start, length, writable: false));
        }
    }
}