using System.Collections.Concurrent;
using System.Text;

namespace AIStudio.Tools;

public static class ContentStreamSseHandler
{
    private static readonly ConcurrentDictionary<string, List<ContentStreamPptxImageData>> PPTX_IMAGES = new();
    private static int CURRENT_SLIDE_NUMBER;

    public static string ProcessEvent(ContentStreamSseEvent? sseEvent, bool extractImages = true)
    {
        switch (sseEvent)
        {
            case { Content: not null, Metadata: not null }:
                switch (sseEvent.Metadata)
                {
                    case ContentStreamTextMetadata:
                        return $"{sseEvent.Content}";
                
                    case ContentStreamPdfMetadata pdfMetadata:
                        var pageNumber = pdfMetadata.Pdf?.PageNumber ?? 0;
                        return $"# Page {pageNumber}\n{sseEvent.Content}";
                    
                    case ContentStreamSpreadsheetMetadata spreadsheetMetadata:
                        var sheetName = spreadsheetMetadata.Spreadsheet?.SheetName;
                        var rowNumber = spreadsheetMetadata.Spreadsheet?.RowNumber;
                        var spreadSheetResult = new StringBuilder();
                        if (rowNumber == 1)
                            spreadSheetResult.AppendLine($"\n# {sheetName}");
                
                        spreadSheetResult.AppendLine($"{sseEvent.Content}");
                        return spreadSheetResult.ToString();
                
                    case ContentStreamDocumentMetadata:
                    case ContentStreamImageMetadata:
                        return $"{sseEvent.Content}";

                    case ContentStreamPresentationMetadata presentationMetadata:
                        var slideNumber = presentationMetadata.Presentation?.SlideNumber ?? 0;
                        var image = presentationMetadata.Presentation?.Image ?? null;
                        var presentationResult = new StringBuilder();
                        if (slideNumber != CURRENT_SLIDE_NUMBER)
                            presentationResult.AppendLine($"# Slide {slideNumber}");
                    
                        presentationResult.Append($"{sseEvent.Content}");

                        if (image is not null)
                        {
                            var isEnd = ProcessImageSegment(image);
                            if (isEnd && extractImages)
                                presentationResult.AppendLine(BuildImage(image.Id!));
                        }
                    
                        CURRENT_SLIDE_NUMBER = slideNumber;
                        return presentationResult.ToString();

                    default:
                        return sseEvent.Content;
                }
                
            case { Content: not null, Metadata: null }:
                return sseEvent.Content;
            
            default:
                return string.Empty;
        }
    }
    
    private static bool ProcessImageSegment(ContentStreamPptxImageData contentStreamPptxImageData)
    {
        if (string.IsNullOrWhiteSpace(contentStreamPptxImageData.Id))
            return false;

        var id =  contentStreamPptxImageData.Id;
        var segment = contentStreamPptxImageData.Segment ?? 0;
        var content = contentStreamPptxImageData.Content ?? string.Empty;
        var isEnd = contentStreamPptxImageData.IsEnd;

        var imageSegment = new ContentStreamPptxImageData
        {
            Id = id,
            Content = content,
            Segment = segment,
            IsEnd = isEnd,
        };
        
        PPTX_IMAGES.AddOrUpdate(
            id,
            _ => [imageSegment],
            (_, existingList) =>
            {
                existingList.Add(imageSegment);
                return existingList;
            }
        );

        return isEnd;
    }

    private static string BuildImage(string id)
    {
        if (!PPTX_IMAGES.TryGetValue(id, out var imageSegments))
            return string.Empty;
        
        var sortedSegments = imageSegments
            .OrderBy(item => item.Segment)
            .ToList();
            
        var base64Image = string.Join(string.Empty, sortedSegments
            .Where(item => item.Content != null)
            .Select(item => item.Content));
        
        PPTX_IMAGES.Remove(id, out _);
        return base64Image;
    }
}