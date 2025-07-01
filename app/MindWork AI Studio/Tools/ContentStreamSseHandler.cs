using System.Collections.Concurrent;
using System.Text;

namespace AIStudio.Tools;

public static class ContentStreamSseHandler
{
    private static readonly ConcurrentDictionary<string, List<ContentStreamPptxImageData>> CHUNKED_IMAGES = new();
    private static readonly ConcurrentDictionary<string, SlideManager> SLIDE_MANAGERS = new();

    public static string? ProcessEvent(ContentStreamSseEvent? sseEvent, bool extractImages = true)
    {
        switch (sseEvent)
        {
            case { Content: not null, Metadata: not null }:
                switch (sseEvent.Metadata)
                {
                    case ContentStreamTextMetadata:
                        return sseEvent.Content;
                
                    case ContentStreamPdfMetadata pdfMetadata:
                        var pageNumber = pdfMetadata.Pdf?.PageNumber ?? 0;
                        return $"""
                                # Page {pageNumber}
                                {sseEvent.Content}
                                
                                """;
                    
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
                        return spreadSheetResult.ToString();
                
                    case ContentStreamDocumentMetadata:
                    case ContentStreamImageMetadata:
                        return sseEvent.Content;

                    case ContentStreamPresentationMetadata presentationMetadata:
                        var slideManager = SLIDE_MANAGERS.GetOrAdd(
                            sseEvent.StreamId!,
                            _ => new()
                        );
                        
                        slideManager.AddSlide(presentationMetadata, sseEvent.Content, extractImages);
                        return null;
                    
                    default:
                        return sseEvent.Content;
                }
                
            case { Content: not null, Metadata: null }:
                return sseEvent.Content;
            
            default:
                return null;
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
        
        SLIDE_MANAGERS.TryRemove(streamId, out _);
        var imageIdPrefix = $"{streamId}-";
        foreach (var key in CHUNKED_IMAGES.Keys.Where(k => k.StartsWith(imageIdPrefix, StringComparison.InvariantCultureIgnoreCase)))
            CHUNKED_IMAGES.TryRemove(key, out _);
        
        return finalContentChunk.Length > 0 ? finalContentChunk.ToString() : null;
    }
}