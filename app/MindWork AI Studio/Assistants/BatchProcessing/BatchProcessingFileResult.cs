namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// The result of processing one file within a batch run.
/// </summary>
public sealed class BatchProcessingFileResult
{
    /// <summary>
    /// The absolute path of the processed file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The file name of the processed file.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The path of the file relative to the input folder. For files directly
    /// inside the input folder, this is the file name.
    /// </summary>
    /// <remarks>
    /// This is the identity of the document within a batch run: it is written
    /// to the log and is used to recognize the document when a previous run is
    /// continued. The file name alone would not be sufficient, because two
    /// subfolders may contain a document of the same name.
    /// </remarks>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The processing state of the file.
    /// </summary>
    public BatchProcessingFileStatus Status { get; set; } = BatchProcessingFileStatus.QUEUED;

    /// <summary>
    /// An optional message, e.g., the error message when the processing failed.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The AI answer for this file.
    /// </summary>
    public string ResultText { get; set; } = string.Empty;

    /// <summary>
    /// The model which produced the answer for this file.
    /// </summary>
    /// <remarks>
    /// We store the model per file instead of reading the currently selected
    /// model when writing the results table. Otherwise, changing the model
    /// between two batch runs would relabel the rows of the previous run.
    /// </remarks>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// The time when the processing of this file finished.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; set; }

    /// <summary>
    /// The tools the model used for this file, ready to be read in the log.
    /// </summary>
    /// <remarks>
    /// Recorded per file, because the model decides per document whether it
    /// needs a tool at all. Without this, a batch run gives no clue why one
    /// answer is better informed than the next.
    /// </remarks>
    public string UsedTools { get; set; } = string.Empty;
}