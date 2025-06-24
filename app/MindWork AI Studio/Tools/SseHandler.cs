using System.Text;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools;

public static class SseHandler
{
    public static async Task<string> ProcessEventAsync(SseEvent? sseEvent)
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
                    result.AppendLine($"[Page {pageNumber}]:\n{sseEvent.Content}");
                    break;
                    
                case SpreadsheetMetadata spreadsheetMetadata:
                    var sheetName = spreadsheetMetadata.Spreadsheet?.SheetName;
                    var rowNumber = spreadsheetMetadata.Spreadsheet?.RowNumber;
                    
                    if (rowNumber == 1) { result.AppendLine($"\n{sheetName}:"); }
                
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case DocumentMetadata documentMetadata:
                    result.AppendLine($"{sseEvent.Content}");
                    break;
                
                case ImageMetadata imageMetadata:
                    result.AppendLine($"{sseEvent.Content}");
                    break;

                default:
                    result.AppendLine(sseEvent.Content);
                    break;
            }
        }
        else if (!string.IsNullOrEmpty(sseEvent.Content))
        {
            result.AppendLine(sseEvent.Content);
        }
        else if (string.IsNullOrEmpty(sseEvent.Content))
        {
            result.AppendLine();
        }

        await Task.CompletedTask;
        return result.ToString();
    }
    
    private static void ProcessImageSegment(PptxImageData pptxImageData, Dictionary<string, List<string>> images, StringBuilder resultBuilder, ILogger logger)
    {
        if (string.IsNullOrEmpty(pptxImageData.Id) || string.IsNullOrEmpty(pptxImageData.Content))
        {
            return;
        }

        if (!images.ContainsKey(pptxImageData.Id))
        {
            images[pptxImageData.Id] = new List<string>();
        }

        images[pptxImageData.Id].Add(pptxImageData.Content);

        if (pptxImageData.IsEnd)
        {
            resultBuilder.AppendLine("[Präsentationsbild eingebettet]");
            // TODO
        }
    }
}