using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// LaTeX documents (.tex), whose paragraphs, headings, captions, list items, table cells, and title a model works on,
/// while the preamble, formulas, code, drawings, and comments stay as they are.
/// </summary>
/// <remarks>
/// <para>
/// The new document is the source with the words of each segment replaced, see LatexSegmentReader. New words are
/// escaped where LaTeX treats a character specially, see LatexWords, and go into the document as UTF-8.
/// </para>
/// <para>
/// The format leaves the language settings alone, e.g., of babel or polyglossia: a document may switch between
/// languages on purpose, and another script may need other packages or fonts, which only its author can choose.
/// The document counts as UTF-8 unless a byte order mark says otherwise; a document in another encoding is refused, see TextDocumentFile.
/// </para>
/// </remarks>
public sealed class LatexSegmentFormat : TextSegmentFormat
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LatexSegmentFormat).Namespace, nameof(LatexSegmentFormat));

    /// <inheritdoc />
    public override FileTypeFilter FileType => FileTypes.TEX;

    /// <inheritdoc />
    public override string LimitsNotice => TB("Formulas, code, drawings, comments, and the preamble apart from the title stay as they are, as does text in commands which the document defines itself. The language settings, e.g., of babel or polyglossia, stay unchanged, and a language with another script may need further packages.");

    /// <inheritdoc />
    protected override IReadOnlyList<TextSegment> ReadSegments(string text) => LatexSegmentReader.SegmentsOf(text);

    /// <inheritdoc />
    protected override string EncodedWords(string words) => LatexWords.Encoded(words);

    /// <inheritdoc />
    protected override string? DeclaredEncodingOf(string text) => LatexSegmentReader.DeclaredEncodingOf(text);
}