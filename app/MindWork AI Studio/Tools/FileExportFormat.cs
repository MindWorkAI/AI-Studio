namespace AIStudio.Tools;

/// <summary>
/// The file formats a chat message can be exported to.
/// </summary>
public enum FileExportFormat
{
    NONE,
    UNKNOWN,

    MICROSOFT_WORD,
    OPEN_DOCUMENT_TEXT,
    LATEX,
    MARKDOWN,
    HTML,
    CSV,
    TSV,
}