using System.Text;
using AIStudio.Settings.DataModel;
using _Imports = MudExtensions._Imports;

namespace AIStudio.Tools;

public static class SseHandler
{
    // public static async Task ProcessEventAsync(SseEvent sseEvent, StringBuilder resultBuilder, Dictionary<string, List<string>> images, ILogger logger)
    // {
    //     if (sseEvent.Metadata != null)
    //     {
    //         await HandleMetadataAsync(sseEvent.Metadata, resultBuilder, images, logger);
    //     }
    //     
    //     if (!string.IsNullOrEmpty(sseEvent.Content))
    //     {
    //         resultBuilder.Append(sseEvent.Content);
    //     }
    // }
    
    public static async Task<string> ProcessEventAsync(SseEvent sseEvent)
    {
        var result = new StringBuilder();

        if (sseEvent == null)
        {
            // Falls `sseEvent` null ist, gib einen leeren String zurück oder handle es entsprechend.
            return result.ToString();
        }

        // Kombiniere Content und Metadata
        if (sseEvent.Content != null && sseEvent.Metadata != null)
        {
            // Je nach Typ der Metadata, verarbeite entsprechend
            switch (sseEvent.Metadata)
            {
                case TextMetadata textMetadata:
                    var lineNumber = textMetadata.Text?.LineNumber ?? 0;
                    result.AppendLine($"{lineNumber}:\t{sseEvent.Content}");
                    break;
                
                case SpreadsheetMetadata spreadsheetMetadata:
                    var sheetName = spreadsheetMetadata.Spreadsheet.SheetName;
                    var rowNumber = spreadsheetMetadata.Spreadsheet.RowNumber;
                    if (rowNumber == 1) { result.AppendLine($"{sheetName}");}
                    
                    result.AppendLine($"{rowNumber}:\t{sseEvent.Content}");
                    break;
                
                case DocumentMetadata documentMetadata:
                    result.AppendLine($"{sseEvent.Content}");
                    break;

                // Weitere Metadaten-Typen können hier hinzugefügt werden
                // case EmbeddingMetadata embeddingMetadata:
                //     // Verarbeitung für EmbeddingMetadata
                //     break;

                default:
                    // Wenn der Metadaten-Typ nicht erkannt wird, füge nur den Content hinzu
                    result.AppendLine(sseEvent.Content);
                    break;
            }
        }
        else if (!string.IsNullOrEmpty(sseEvent.Content))
        {
            // Falls nur Content vorhanden ist
            result.AppendLine(sseEvent.Content);
        }
        else if (string.IsNullOrEmpty(sseEvent.Content))
        {
            result.AppendLine();
        }

        // Asynchrone Verarbeitung, falls erforderlich
        await Task.CompletedTask; // Placeholder für asynchrone Operationen

        return result.ToString();
    }

    private static async Task HandleMetadataAsync(Settings.DataModel.Metadata metadata, StringBuilder resultBuilder, Dictionary<string, List<string>> images, ILogger logger)
    {
        switch (metadata)
        {
            case TextMetadata textMetadata:
                // Für Textdateien: Zeilennummer hinzufügen
                resultBuilder.AppendLine();
                if (textMetadata.Text != null)
                {
                    var lineNumber = textMetadata.Text.LineNumber;
                    resultBuilder.AppendLine($"[Zeile {lineNumber}]");
                }
                break;

            case PdfMetadata pdfMetadata:
                // Für PDF: Seitennummer und Umbruch
                var pageNumber = pdfMetadata.Pdf.PageNumber;
                resultBuilder.AppendLine();
                resultBuilder.AppendLine($"[Seite {pageNumber}]");
                break;

            case SpreadsheetMetadata spreadsheetMetadata:
                // Für Tabellen: Arbeitsblattname und Zeilennummer
                var sheetName = spreadsheetMetadata.Spreadsheet.SheetName;
                var rowNumber = spreadsheetMetadata.Spreadsheet.RowNumber;
                resultBuilder.AppendLine();
                resultBuilder.AppendLine($"[Tabelle: {sheetName}, Zeile: {rowNumber}]");
                break;

            case PresentationMetadata presentationMetadata:
                // Für Präsentationen: Foliennummer und ggf. Bild
                var slideNumber = presentationMetadata.Presentation.SlideNumber;
                resultBuilder.AppendLine();
                resultBuilder.AppendLine($"[Folie {slideNumber}]");

                if (presentationMetadata.Presentation.Image != null)
                {
                    ProcessImageSegment(presentationMetadata.Presentation.Image, images, resultBuilder, logger);
                }
                break;

            case ImageMetadata _:
                // Für Bilder
                resultBuilder.AppendLine();
                resultBuilder.AppendLine("[Bildbeschreibung]");
                break;

            default:
                // Unbekannter Metadaten-Typ
                logger?.LogWarning("Unbekannter Metadaten-Typ: {Type}", metadata.GetType().Name);
                break;
        }

        await Task.CompletedTask;
    }

    private static void ProcessImageSegment(ImageData imageData, Dictionary<string, List<string>> images, StringBuilder resultBuilder, ILogger logger)
    {
        if (string.IsNullOrEmpty(imageData.Id) || string.IsNullOrEmpty(imageData.Content))
        {
            return;
        }

        if (!images.ContainsKey(imageData.Id))
        {
            images[imageData.Id] = new List<string>();
        }

        images[imageData.Id].Add(imageData.Content);

        logger?.LogDebug("Added image segment {Segment} for image {Id}", imageData.Segment, imageData.Id);

        if (imageData.IsEnd)
        {
            logger?.LogDebug("Completed image {Id} with {SegmentCount} segments", imageData.Id, images[imageData.Id].Count);
            resultBuilder.AppendLine("[Präsentationsbild eingebettet]");

            // Hier kannst du die Bilddaten weiterverarbeiten, z.B. zusammenfügen und speichern
        }
    }
}