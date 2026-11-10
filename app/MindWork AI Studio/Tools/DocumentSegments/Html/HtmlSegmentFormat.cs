using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.DocumentSegments.Html;

/// <summary>
/// Web pages (.html, .htm), whose blocks, title, description, and readable attributes a model works on, while the
/// markup, scripts, styles, and code stay as they are.
/// </summary>
/// <remarks>
/// <para>
/// The new page is the source with the words of each segment replaced, see HtmlSegmentReader. A readable attribute,
/// e.g., the alternative text of a picture, may lie within the markup of a block's segment, e.g., a picture in a
/// paragraph. So the writer replaces the attributes first, reads the blocks of that page again, which yields the
/// same segments with the new attributes in their markup, and replaces the blocks then.
/// </para>
/// <para>
/// The page counts as UTF-8 unless a byte order mark says otherwise, see TextDocumentFile.
/// </para>
/// </remarks>
public sealed class HtmlSegmentFormat : IDocumentSegmentFormat
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(HtmlSegmentFormat).Namespace, nameof(HtmlSegmentFormat));

    /// <inheritdoc />
    public FileTypeFilter FileType => FileTypes.HTML;

    /// <inheritdoc />
    public string LimitsNotice => TB("Scripts, styles, code, preformatted text, and embedded graphics stay as they are. Pages in an older encoding than UTF-8 may show wrong characters.");

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSegment>> ReadAsync(string path, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(path, token);
        return HtmlSegmentReader.AttributeSegmentsOf(file.Text)
            .Concat(HtmlSegmentReader.BlockSegmentsOf(file.Text))
            .OrderBy(segment => segment.Start)
            .Select(segment => segment.Segment)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SegmentWriteResult> WriteAsync(string sourcePath, string targetPath, SegmentResults results, string languageTag, ILogger logger, CancellationToken token)
    {
        var file = await TextDocumentFile.ReadAsync(sourcePath, token);
        var attributes = HtmlSegmentReader.AttributeSegmentsOf(file.Text);
        if (HtmlSegmentReader.BlockSegmentsOf(file.Text).Concat(attributes).Any(segment => !results.Covers(segment.Segment)))
            return SegmentWriteResult.SOURCE_CHANGED;

        var attributesResult = TextSegmentWriter.Apply(file.Text, attributes, results, HtmlSegmentReader.EncodedAttribute, out var withAttributes);
        var blocksResult = TextSegmentWriter.Apply(withAttributes, HtmlSegmentReader.BlockSegmentsOf(withAttributes), results, HtmlSegmentReader.EncodedText, out var withBlocks);
        if (blocksResult.Outcome is SegmentWriteOutcome.SOURCE_CHANGED)
        {
            logger.LogWarning("The blocks of the page '{SourcePath}' differ once its attributes are replaced, so nothing is written.", sourcePath);
            return SegmentWriteResult.SOURCE_CHANGED;
        }

        await file.WriteAsync(targetPath, WithLanguage(withBlocks, languageTag), token);
        return new(SegmentWriteOutcome.WRITTEN, attributesResult.ExactCount + blocksResult.ExactCount, attributesResult.SimplifiedCount + blocksResult.SimplifiedCount);
    }

    /// <summary>
    /// Notes the language of the page in its root element, so that browsers and screen readers follow it.
    /// </summary>
    /// <param name="text">The page.</param>
    /// <param name="languageTag">The IETF tag of the language; empty to leave the page as it is.</param>
    private static string WithLanguage(string text, string languageTag)
    {
        if (languageTag.Length == 0 || HtmlSegmentReader.Parse(text).DocumentNode.Descendants("html").FirstOrDefault() is not { OuterStartIndex: >= 0 } root)
            return text;

        if (root.Attributes["lang"] is { ValueStartIndex: > 0, ValueLength: >= 0 } language && language.ValueStartIndex + language.ValueLength <= text.Length)
            return string.Concat(text.AsSpan(0, language.ValueStartIndex), languageTag, text.AsSpan(language.ValueStartIndex + language.ValueLength));

        const string ROOT_START = "<html";
        if (root.OuterStartIndex + ROOT_START.Length > text.Length || !text.AsSpan(root.OuterStartIndex, ROOT_START.Length).Equals(ROOT_START, StringComparison.OrdinalIgnoreCase))
            return text;

        var insertAt = root.OuterStartIndex + ROOT_START.Length;
        return string.Concat(text.AsSpan(0, insertAt), $" lang=\"{languageTag}\"", text.AsSpan(insertAt));
    }
}