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
/// The document counts as UTF-8 unless a byte order mark says otherwise, see TextDocumentFile.
/// </para>
/// </remarks>
public sealed class LatexSegmentFormat : IDocumentSegmentFormat
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LatexSegmentFormat).Namespace, nameof(LatexSegmentFormat));

    /// <inheritdoc />
    public FileTypeFilter FileType => FileTypes.TEX;

    /// <inheritdoc />
    public string LimitsNotice => TB("Formulas, code, drawings, comments, and the preamble apart from the title stay as they are, as does text in commands which the document defines itself. The language settings, e.g., of babel or polyglossia, stay unchanged, and a language with another script may need further packages. Documents in an older encoding than UTF-8 may show wrong characters.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(path, token);
        return LatexSegmentReader.SegmentsOf(file.Text).Select(segment => segment.Segment).ToList();
    }

    /// <inheritdoc />
    public async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, ILogger logger, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(sourcePath, token);
        var result = TextSegmentWriter.Apply(file.Text, LatexSegmentReader.SegmentsOf(file.Text), results, LatexWords.Encoded, out var text);
        if (result.Outcome is SegmentWriteOutcome.WRITTEN)
            await file.WriteAsync(targetPath, text, token);

        return result;
    }
}