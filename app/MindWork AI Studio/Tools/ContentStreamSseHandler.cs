using System.Collections.Concurrent;
using System.Text;

namespace AIStudio.Tools;

public static class ContentStreamSseHandler
{
    private static readonly ConcurrentDictionary<string, List<ContentStreamPptxImageData>> CHUNKED_IMAGES = new();
    private static readonly ConcurrentDictionary<string, SlideManager> SLIDE_MANAGERS = new();
    private static readonly ConcurrentDictionary<string, DocumentManager> DOCUMENT_MANAGERS = new();

    public static ContentStreamProcessedEvent ProcessEvent(ContentStreamSseEvent? sseEvent, bool extractImages = true)
    {
        switch (sseEvent)
        {
            case { Content: not null, Metadata: not null }:
                switch (sseEvent.Metadata)
                {
                    case ContentStreamTextMetadata:
                        return ContentStreamProcessedEvent.FromContent(sseEvent.Content);

                    case ContentStreamPdfMetadata pdfMetadata:
                        var pageNumber = pdfMetadata.Pdf?.PageNumber ?? 0;
                        return ContentStreamProcessedEvent.FromContent($"""
                                # Page {pageNumber}
                                {sseEvent.Content}

                                """);

                    case ContentStreamSpreadsheetMetadata spreadsheetMetadata:
                        var sheetName = spreadsheetMetadata.Spreadsheet?.SheetName;
                        var rowNumber = spreadsheetMetadata.Spreadsheet?.RowNumber;
                        var spreadSheetResult = new StringBuilder();
                        if (rowNumber == 0)
                        {
                            spreadSheetResult.AppendLine();
                            spreadSheetResult.AppendLine($"# {sheetName}");
                        }

                        spreadSheetResult.Append(sseEvent.Content);
                        return ContentStreamProcessedEvent.FromContent(spreadSheetResult.ToString());

                    //
                    // Documents which the runtime reads page by page are buffered, so the images of
                    // a page can follow its Markdown. Documents converted as a whole, e.g. by Pandoc,
                    // carry no page number and are passed on unchanged.
                    //
                    case ContentStreamDocumentMetadata documentMetadata:
                        if (documentMetadata.Document?.PageNumber is not > 0)
                            return ContentStreamProcessedEvent.FromContent(sseEvent.Content);

                        var documentManager = DOCUMENT_MANAGERS.GetOrAdd(sseEvent.StreamId!, _ => new());
                        var documentContent = documentManager.AddPage(documentMetadata, sseEvent.Content, extractImages);
                        return documentContent is null ? ContentStreamProcessedEvent.NOTHING : ContentStreamProcessedEvent.FromContent(documentContent);

                    case ContentStreamImageMetadata:
                        return ContentStreamProcessedEvent.FromContent(sseEvent.Content);

                    case ContentStreamPresentationMetadata presentationMetadata:
                        var slideManager = SLIDE_MANAGERS.GetOrAdd(
                            sseEvent.StreamId!,
                            _ => new()
                        );

                        slideManager.AddSlide(presentationMetadata, sseEvent.Content, extractImages);
                        return ContentStreamProcessedEvent.NOTHING;

                    //
                    // The runtime reported a failure. It must not contribute any content: an empty
                    // or partial document would otherwise be handed to the AI as if it were the
                    // real file content.
                    //
                    case ContentStreamErrorMetadata errorMetadata:
                        return ContentStreamProcessedEvent.FromError(errorMetadata.Error);

                    //
                    // The runtime filtered suspected prompt injections out of the content. The
                    // content itself already arrived through the events before this one, so this
                    // only reports what was removed.
                    //
                    case ContentStreamPromptInjectionMetadata promptInjectionMetadata:
                        return ContentStreamProcessedEvent.FromPromptInjection(promptInjectionMetadata.PromptInjection);

                    default:
                        return ContentStreamProcessedEvent.FromContent(sseEvent.Content);
                }

            case { Content: not null, Metadata: null }:
                return ContentStreamProcessedEvent.FromContent(sseEvent.Content);

            default:
                return ContentStreamProcessedEvent.NOTHING;
        }
    }

    public static bool ProcessImageSegment(string imageId, ContentStreamPptxImageData contentStreamPptxImageData)
    {
        if (string.IsNullOrWhiteSpace(contentStreamPptxImageData.Id) || string.IsNullOrWhiteSpace(imageId))
            return false;

        var segment = contentStreamPptxImageData.Segment ?? 0;
        var content = contentStreamPptxImageData.Content ?? string.Empty;
        var isEnd = contentStreamPptxImageData.IsEnd;

        var imageSegment = new ContentStreamPptxImageData
        {
            Id = imageId,
            Content = content,
            Segment = segment,
            IsEnd = isEnd,
            MediaType = contentStreamPptxImageData.MediaType,
        };
        
        CHUNKED_IMAGES.AddOrUpdate(
            imageId,
            _ => [imageSegment],
            (_, existingList) =>
            {
                existingList.Add(imageSegment);
                return existingList;
            }
        );

        return isEnd;
    }

    public static string BuildImage(string id)
    {
        if (!CHUNKED_IMAGES.TryGetValue(id, out var imageSegments))
            return string.Empty;
        
        var sortedSegments = imageSegments
            .OrderBy(item => item.Segment)
            .ToList();
            
        var base64Image = string.Join(string.Empty, sortedSegments
            .Where(item => item.Content != null)
            .Select(item => item.Content));
        
        CHUNKED_IMAGES.Remove(id, out _);
        return base64Image;
    }

    /// <summary>
    /// Assembles the collected segments of an image into a Markdown image.
    /// </summary>
    /// <remarks>
    /// Handing the naked Base64 data to the AI says nothing: it is neither readable text nor an
    /// image it could look at. Only the data URI makes it one, so every reader must embed its
    /// images this way.
    /// </remarks>
    /// <param name="id">The ID of the image to assemble.</param>
    /// <param name="mediaType">The media type the runtime reported, if any.</param>
    /// <returns>The Markdown image, or null when no data was collected for that ID.</returns>
    public static string? BuildImageMarkdown(string id, string? mediaType)
    {
        var base64Image = BuildImage(id);
        if (string.IsNullOrWhiteSpace(base64Image))
            return null;

        //
        // Both readers compress their images, and that compression produces JPEG. A runtime which
        // does not report the media type therefore delivered JPEG as well.
        //
        var imageMediaType = string.IsNullOrWhiteSpace(mediaType) ? "image/jpeg" : mediaType;
        return $"![Image](data:{imageMediaType};base64,{base64Image})";
    }

    public static string? Clear(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId))
            return null;
 
        var finalContentChunk = new StringBuilder();
        if(SLIDE_MANAGERS.TryGetValue(streamId, out var slideManager))
        {
            var result = slideManager.GetAllSlidesInOrder();
            if (!string.IsNullOrWhiteSpace(result))
                finalContentChunk.Append(result);
        }

        if (DOCUMENT_MANAGERS.TryGetValue(streamId, out var documentManager))
        {
            var result = documentManager.Flush();
            if (!string.IsNullOrWhiteSpace(result))
                finalContentChunk.Append(result);
        }
        
        SLIDE_MANAGERS.TryRemove(streamId, out _);
        DOCUMENT_MANAGERS.TryRemove(streamId, out _);
        var imageIdPrefix = $"{streamId}-";
        foreach (var key in CHUNKED_IMAGES.Keys.Where(k => k.StartsWith(imageIdPrefix, StringComparison.InvariantCultureIgnoreCase)))
            CHUNKED_IMAGES.TryRemove(key, out _);
        
        return finalContentChunk.Length > 0 ? finalContentChunk.ToString() : null;
    }
}
