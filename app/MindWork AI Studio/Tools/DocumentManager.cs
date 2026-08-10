using System.Text;

namespace AIStudio.Tools;

/// <summary>
/// Buffers only the active document page so that its image segments can follow
/// the page Markdown without retaining the complete document in memory.
/// </summary>
public sealed class DocumentManager
{
    private StringBuilder? currentPageContent;

    public string? AddPage(ContentStreamDocumentMetadata metadata, string? content, bool extractImages)
    {
        var pageNumber = metadata.Document?.PageNumber ?? 0;
        if (pageNumber == 0)
            return content;

        var image = metadata.Document?.Image;
        if (image is null)
        {
            var completedPage = this.Flush();
            this.currentPageContent = new StringBuilder();
            this.currentPageContent.AppendLine($"# Page {pageNumber}");
            this.currentPageContent.Append(content);
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
            }
        }

        return null;
    }

    public string? Flush()
    {
        if (this.currentPageContent is null)
            return null;

        var result = this.currentPageContent.ToString();
        this.currentPageContent = null;
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
