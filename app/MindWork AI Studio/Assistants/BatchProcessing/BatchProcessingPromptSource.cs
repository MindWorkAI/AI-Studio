namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// The source of the instructions used to process each document of a batch run.
/// </summary>
public enum BatchProcessingPromptSource
{
    FREE_PROMPT,
    POLICY,
    FILE_IMPORT,
}