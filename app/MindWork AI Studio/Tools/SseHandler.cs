using System.Collections.Concurrent;
using System.Text;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools;

public static class SseHandler
{
    private static readonly ConcurrentDictionary<string, List<PptxImageData>> PPTX_IMAGES = new();
    private static int CURRENT_SLIDE_NUMBER;

    public static async Task<string> ProcessEventAsync(SseEvent? sseEvent, bool extractImages = true)
    {
        var result = new StringBuilder();

        if (sseEvent == null) { return result.ToString(); }

        if (sseEvent is { Content: not null, Metadata: not null })
        {
            switch (sseEvent.Metadata)
            {
                case TextMetadata textMetadata:
                    var lineNumber = textMetadata.Text?.LineNumber ?? 0;
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case PdfMetadata pdfMetadata:
                    var pageNumber = pdfMetadata.Pdf?.PageNumber ?? 0;
                    result.AppendLine($"# Page {pageNumber}\n{sseEvent.Content}");
                    break;
                    
                case SpreadsheetMetadata spreadsheetMetadata:
                    var sheetName = spreadsheetMetadata.Spreadsheet?.SheetName;
                    var rowNumber = spreadsheetMetadata.Spreadsheet?.RowNumber;
                    
                    if (rowNumber == 1) { result.AppendLine($"\n# {sheetName}"); }
                
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case DocumentMetadata documentMetadata:
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case ImageMetadata imageMetadata:
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case PresentationMetadata presentationMetadata:
                    var slideNumber = presentationMetadata.Presentation?.SlideNumber ?? 0;
                    var image = presentationMetadata.Presentation?.Image ?? null;

                    if (slideNumber != CURRENT_SLIDE_NUMBER) { result.AppendLine($"# Slide {slideNumber}"); }
                    result.Append($"{sseEvent.Content}");

                    if (image != null)
                    {
                        var isEnd = ProcessImageSegment(image);
                        if (isEnd && extractImages) { result.AppendLine(BuildImage(image.Id!)); }
                    }
                    
                    CURRENT_SLIDE_NUMBER = slideNumber;
                    break;

                default:
                    result.AppendLine(sseEvent.Content);
                    break;
            }
        }
        else if (!string.IsNullOrEmpty(sseEvent.Content))
        {
            result.Append(sseEvent.Content);
        }
        else if (string.IsNullOrEmpty(sseEvent.Content))
        {
            result.Append(string.Empty);
        }

        await Task.CompletedTask;
        return result.ToString();
    }
    
    private static bool ProcessImageSegment(PptxImageData pptxImageData)
    {
        if (string.IsNullOrEmpty(pptxImageData.Id)) { return false; }

        var id =  pptxImageData.Id;
        var segment = pptxImageData.Segment ?? 0;
        var content = pptxImageData.Content ?? string.Empty;
        var isEnd = pptxImageData.IsEnd;

        var imageSegment = new PptxImageData()
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
        if (!PPTX_IMAGES.TryGetValue(id, out var imageSegments)) return string.Empty;
        
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