using System.Text;

using AIStudio.Tools.DocumentSegments;
using AIStudio.Tools.DocumentSegments.Html;
using AIStudio.Tools.DocumentSegments.Latex;

namespace AIStudio.Tests.Tools.DocumentSegments;

/// <summary>
/// Checks that a document which is text itself keeps its bytes, and that one which would lose characters is refused.
/// </summary>
[TestFixture]
public sealed class TextDocumentFileTests
{
    /// <summary>
    /// Characters of several scripts, a decomposed ä, characters beyond the BMP, and line endings of Windows.
    /// </summary>
    private const string TEXT = "ä ß € é ø ş ł ж 日本 한\r\na\u0308 𝄞 😀\r\nEnd";

    private static readonly byte[] WINDOWS_1252_BYTES = [(byte)'G', (byte)'r', 0xFC, 0xDF, (byte)'e'];

    private string testFolder = string.Empty;

    [SetUp]
    public void CreateFolder() => this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void RemoveFolder() => Directory.Delete(this.testFolder, true);

    private static IEnumerable<TestCaseData> UnicodeEncodings()
    {
        yield return new TestCaseData(new UTF8Encoding(false)).SetArgDisplayNames("UTF-8 without a byte order mark");
        yield return new TestCaseData(new UTF8Encoding(true)).SetArgDisplayNames("UTF-8 with a byte order mark");
        yield return new TestCaseData(new UnicodeEncoding(false, true)).SetArgDisplayNames("UTF-16 little endian");
        yield return new TestCaseData(new UnicodeEncoding(true, true)).SetArgDisplayNames("UTF-16 big endian");
        yield return new TestCaseData(new UTF32Encoding(false, true)).SetArgDisplayNames("UTF-32 little endian");
        yield return new TestCaseData(new UTF32Encoding(true, true)).SetArgDisplayNames("UTF-32 big endian");
    }

    [TestCaseSource(nameof(UnicodeEncodings))]
    public async Task ADocumentInUnicodeKeepsItsBytes(Encoding encoding)
    {
        var sourceBytes = encoding.GetPreamble().Concat(encoding.GetBytes(TEXT)).ToArray();
        var source = await this.WriteAsync("source.txt", sourceBytes);
        var target = Path.Join(this.testFolder, "target.txt");

        var file = await TextDocumentFile.ReadAsync(source, null, CancellationToken.None);
        await file.WriteAsync(target, file.Text, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(file.Text, Is.EqualTo(TEXT), "The decomposed ä stays decomposed, and the line endings stay as they are.");
            Assert.That(await File.ReadAllBytesAsync(target), Is.EqualTo(sourceBytes));
        }
    }

    [Test]
    public async Task ADocumentInWindows1252IsRefused()
    {
        var source = await this.WriteAsync("source.txt", WINDOWS_1252_BYTES);

        var exception = await Assert.ThrowsAsync<UnsupportedTextEncodingException>(() => TextDocumentFile.ReadAsync(source, null, CancellationToken.None));

        Assert.That(exception!.DeclaredEncoding, Is.Empty);
    }

    [Test]
    public async Task ADeclaredEncodingOtherThanUtf8IsRefused()
    {
        var source = await this.WriteAsync("page.html", Encoding.ASCII.GetBytes("<html><head><meta charset=\"iso-8859-1\"></head><body><p>M&uuml;ller</p></body></html>"));

        var exception = await Assert.ThrowsAsync<UnsupportedTextEncodingException>(() => new HtmlSegmentFormat().ReadAsync(source, CancellationToken.None));

        Assert.That(exception!.DeclaredEncoding, Is.EqualTo("iso-8859-1"), "An ASCII page which says Latin-1 would show new words in UTF-8 garbled.");
    }

    [Test]
    public async Task AByteOrderMarkBeatsTheDeclaredEncoding()
    {
        var page = "<html><head><meta charset=\"iso-8859-1\"></head><body><p>Grüße</p></body></html>";
        var source = await this.WriteAsync("page.html", new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(page)).ToArray());

        var segments = await new HtmlSegmentFormat().ReadAsync(source, CancellationToken.None);

        Assert.That(segments.Select(segment => segment.Key), Does.Contain("Grüße"), "Browsers follow the byte order mark, too.");
    }

    [Test]
    public async Task ALatexDocumentInLatin1IsRefused()
    {
        var source = await this.WriteAsync("paper.tex", Encoding.ASCII.GetBytes("\\documentclass{article}\n\\usepackage[latin1]{inputenc}\n\\begin{document}\nHello.\n\\end{document}"));

        var exception = await Assert.ThrowsAsync<UnsupportedTextEncodingException>(() => new LatexSegmentFormat().ReadAsync(source, CancellationToken.None));

        Assert.That(exception!.DeclaredEncoding, Is.EqualTo("latin1"));
    }

    [TestCase("UTF-8", ExpectedResult = true)]
    [TestCase(" utf8 ", ExpectedResult = true)]
    [TestCase("utf8x", ExpectedResult = true)]
    [TestCase("unicode-1-1-utf-8", ExpectedResult = true)]
    [TestCase("iso-8859-1", ExpectedResult = false)]
    [TestCase("latin1", ExpectedResult = false)]
    [TestCase("windows-1252", ExpectedResult = false)]
    [TestCase("utf-16", ExpectedResult = false)]
    [TestCase("", ExpectedResult = false)]
    public bool TheNamesOfUtf8AreKnown(string name) => TextDocumentFile.IsUtf8(name);

    [TestCase("<meta charset=\"ISO-8859-1\">", ExpectedResult = "ISO-8859-1")]
    [TestCase("<meta charset=utf-8>", ExpectedResult = "utf-8")]
    [TestCase("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=windows-1252\">", ExpectedResult = "windows-1252")]
    [TestCase("<?xml version=\"1.0\" encoding=\"iso-8859-15\"?><html></html>", ExpectedResult = "iso-8859-15")]
    [TestCase("<meta name=\"description\" content=\"charset=latin1\">", ExpectedResult = null)]
    [TestCase("<p>No encoding named.</p>", ExpectedResult = null)]
    public string? AWebPageNamesItsEncoding(string page) => HtmlSegmentReader.DeclaredEncodingOf(page);

    [TestCase("\\usepackage[latin1]{inputenc}", ExpectedResult = "latin1")]
    [TestCase("\\usepackage[utf8]{inputenc}", ExpectedResult = "utf8")]
    [TestCase("% \\usepackage[latin1]{inputenc}\n\\usepackage[utf8]{inputenc}", ExpectedResult = "utf8")]
    [TestCase("\\usepackage[utf8]{inputenc}\n\\inputencoding{latin9}", ExpectedResult = "latin9")]
    [TestCase("\\usepackage[T1]{fontenc}", ExpectedResult = null)]
    [TestCase("50\\% \\usepackage[latin1]{inputenc}", ExpectedResult = "latin1")]
    public string? ALatexDocumentNamesItsEncoding(string document) => LatexSegmentReader.DeclaredEncodingOf(document);

    private async Task<string> WriteAsync(string name, byte[] bytes)
    {
        var path = Path.Join(this.testFolder, name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}