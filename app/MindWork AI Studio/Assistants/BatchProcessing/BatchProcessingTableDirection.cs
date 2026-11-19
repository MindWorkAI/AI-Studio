namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// How a batch run goes through a table.
/// </summary>
public enum BatchProcessingTableDirection
{
    /// <summary>
    /// Each row is a document. The first row holds the headings of the columns.
    /// </summary>
    ROWS,

    /// <summary>
    /// Each column is a document. The first column holds the headings of the rows.
    /// </summary>
    COLUMNS,
}