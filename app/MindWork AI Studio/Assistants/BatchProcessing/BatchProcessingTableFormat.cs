namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// The file format of the results table, which the output mode TABLE_ONLY writes.
/// </summary>
public enum BatchProcessingTableFormat
{
    /// <summary>
    /// A CSV file with the chosen column separator.
    /// </summary>
    /// <remarks>
    /// This must stay the first member: an unknown name falls back to the member with the value
    /// zero, and settings written before the Excel format existed mean CSV.
    /// </remarks>
    CSV,

    /// <summary>
    /// A Microsoft Excel workbook (.xlsx) with one worksheet.
    /// </summary>
    EXCEL,
}