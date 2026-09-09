using System.Text;

namespace AIStudio.Tools;

/// <summary>
/// Buffers only the active document page so that its image segments can follow
/// the page Markdown without retaining the complete document in memory.
/// </summary>
public sealed class DocumentManager
{
    private StringBuilder? currentPageContent;
    private int? currentPageTokenCount;

    public ContentStreamPendingContent? AddPage(ContentStreamDocumentMetadata metadata, string? content, int? tokenCount, bool extractImages)
    {
        var pageNumber = metadata.Document?.PageNumber ?? 0;
        if (pageNumber == 0)
            return content is null ? null : new ContentStreamPendingContent(content, tokenCount);

        var image = metadata.Document?.Image;
        if (image is null)
        {
            var completedPage = this.Flush();
            this.currentPageContent = new StringBuilder();

            //
            // A Word or OpenDocument file carries no fixed page layout, so the runtime derives these
            // boundaries from page breaks and heuristics. We note the estimate as a comment rather
            // than as a heading: a heading would sit on the same level as the document's own first
            // level headings, leaving the AI unable to tell the structure of the document apart from
            // our boundaries. The presentation reader marks its slides the same way.
            //
            this.currentPageContent.AppendLine($"<!-- Estimated page {pageNumber} -->");
            this.currentPageContent.AppendLine();
            this.currentPageContent.Append(content);

            //
            // The count waits here together with the page it belongs to. Handing it out along with
            // the page we just completed would size that page by the text of this one.
            //
            this.currentPageTokenCount = tokenCount;
            return completedPage;
        }

        if (!extractImages || this.currentPageContent is null || string.IsNullOrWhiteSpace(image.Id))
            return null;

        if (ContentStreamSseHandler.ProcessImageSegment(image.Id, image))
        {
            var markdownImage = ContentStreamSseHandler.BuildImageMarkdown(image.Id, image.MediaType);
            if (markdownImage is not null)
            {
                this.currentPageContent.AppendLine();
                this.currentPageContent.AppendLine(markdownImage);

                //
                // The runtime counted the text of this page, not the image we just embedded into it.
                // A data URI is orders of magnitude larger than that text, so the count no longer
                // describes the page: we drop it, and whoever needs one counts the page itself.
                //
                this.currentPageTokenCount = null;
            }
        }

        return null;
    }

    public ContentStreamPendingContent? Flush()
    {
        if (this.currentPageContent is null)
            return null;

        var result = this.currentPageContent.ToString();
        var tokenCount = this.currentPageTokenCount;
        this.currentPageContent = null;
        this.currentPageTokenCount = null;
        return string.IsNullOrWhiteSpace(result) ? null : new ContentStreamPendingContent(result, tokenCount);
    }
}
