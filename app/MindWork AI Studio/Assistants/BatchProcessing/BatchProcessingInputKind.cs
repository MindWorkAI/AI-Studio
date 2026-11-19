namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// What a batch run goes through: the documents of a folder, or the rows or columns of a table.
/// </summary>
public enum BatchProcessingInputKind
{
    /// <summary>
    /// The files and subfolders of a folder, each of them a document.
    /// </summary>
    FOLDER,

    /// <summary>
    /// A CSV file, an Excel workbook, or an OpenDocument spreadsheet, whose rows or columns are the documents.
    /// </summary>
    TABLE,
}