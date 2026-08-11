using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    private static readonly AssistantSessionStateKey<string> INPUT_DIRECTORY_STATE_KEY = new(nameof(inputDirectory));
    private static readonly AssistantSessionStateKey<string> OUTPUT_DIRECTORY_STATE_KEY = new(nameof(outputDirectory));
    private static readonly AssistantSessionStateKey<string> FILE_PATTERNS_STATE_KEY = new(nameof(filePatterns));
    private static readonly AssistantSessionStateKey<bool> INCLUDE_SUBDIRECTORIES_STATE_KEY = new(nameof(includeSubdirectories));
    private static readonly AssistantSessionStateKey<BatchProcessingPromptSource> PROMPT_SOURCE_STATE_KEY = new(nameof(promptSource));
    private static readonly AssistantSessionStateKey<string> FREE_PROMPT_STATE_KEY = new(nameof(freePrompt));
    private static readonly AssistantSessionStateKey<string> IMPORTED_PROMPT_STATE_KEY = new(nameof(importedPrompt));
    private static readonly AssistantSessionStateKey<string> PROMPT_FILE_PATH_STATE_KEY = new(nameof(promptFilePath));
    private static readonly AssistantSessionStateKey<string> PROMPT_FILE_LOAD_ISSUE_STATE_KEY = new(nameof(promptFileLoadIssue));
    private static readonly AssistantSessionStateKey<DataDocumentAnalysisPolicy?> SELECTED_POLICY_STATE_KEY = new(nameof(selectedPolicy));
    private static readonly AssistantSessionStateKey<BatchProcessingOutputMode> OUTPUT_MODE_STATE_KEY = new(nameof(outputMode));
    private static readonly AssistantSessionStateKey<string> RESULT_COLUMN_HEADER_STATE_KEY = new(nameof(resultColumnHeader));
    private static readonly AssistantSessionStateKey<string> CSV_FILE_NAME_STATE_KEY = new(nameof(csvFileName));
    private static readonly AssistantSessionStateKey<BatchProcessingCsvSeparator> CSV_SEPARATOR_STATE_KEY = new(nameof(csvSeparator));
    private static readonly AssistantSessionStateKey<string> CUSTOM_CSV_SEPARATOR_STATE_KEY = new(nameof(customCsvSeparator));
    private static readonly AssistantSessionStateKey<List<BatchProcessingFileResult>> FILE_RESULTS_STATE_KEY = new(nameof(fileResults));
    private static readonly AssistantSessionStateKey<HashSet<string>> USED_RESULT_FILE_NAMES_STATE_KEY = new(nameof(usedResultFileNames));
    private static readonly AssistantSessionStateKey<bool> IS_PROCESSING_BATCH_STATE_KEY = new(nameof(isProcessingBatch));
    private static readonly AssistantSessionStateKey<bool> HAS_REPORTED_WRITE_FAILURE_STATE_KEY = new(nameof(hasReportedWriteFailure));
    private static readonly AssistantSessionStateKey<int> NUM_PROCESSED_FILES_STATE_KEY = new(nameof(numProcessedFiles));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_DIRECTORY_STATE_KEY, this.inputDirectory);
        state.Set(OUTPUT_DIRECTORY_STATE_KEY, this.outputDirectory);
        state.Set(FILE_PATTERNS_STATE_KEY, this.filePatterns);
        state.Set(INCLUDE_SUBDIRECTORIES_STATE_KEY, this.includeSubdirectories);
        state.Set(PROMPT_SOURCE_STATE_KEY, this.promptSource);
        state.Set(FREE_PROMPT_STATE_KEY, this.freePrompt);
        state.Set(IMPORTED_PROMPT_STATE_KEY, this.importedPrompt);
        state.Set(PROMPT_FILE_PATH_STATE_KEY, this.promptFilePath);
        state.Set(PROMPT_FILE_LOAD_ISSUE_STATE_KEY, this.promptFileLoadIssue);
        state.Set(SELECTED_POLICY_STATE_KEY, this.selectedPolicy);
        state.Set(OUTPUT_MODE_STATE_KEY, this.outputMode);
        state.Set(RESULT_COLUMN_HEADER_STATE_KEY, this.resultColumnHeader);
        state.Set(CSV_FILE_NAME_STATE_KEY, this.csvFileName);
        state.Set(CSV_SEPARATOR_STATE_KEY, this.csvSeparator);
        state.Set(CUSTOM_CSV_SEPARATOR_STATE_KEY, this.customCsvSeparator);
        state.SetList(FILE_RESULTS_STATE_KEY, this.fileResults.Select(CloneFileResult));
        state.SetHashSet(USED_RESULT_FILE_NAMES_STATE_KEY, this.usedResultFileNames);
        state.Set(IS_PROCESSING_BATCH_STATE_KEY, this.isProcessingBatch);
        state.Set(HAS_REPORTED_WRITE_FAILURE_STATE_KEY, this.hasReportedWriteFailure);
        state.Set(NUM_PROCESSED_FILES_STATE_KEY, this.numProcessedFiles);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_DIRECTORY_STATE_KEY, value => this.inputDirectory = value);
        state.Restore(OUTPUT_DIRECTORY_STATE_KEY, value => this.outputDirectory = value);
        state.Restore(FILE_PATTERNS_STATE_KEY, value => this.filePatterns = value);
        state.Restore(INCLUDE_SUBDIRECTORIES_STATE_KEY, value => this.includeSubdirectories = value);
        state.Restore(PROMPT_SOURCE_STATE_KEY, value => this.promptSource = value);
        state.Restore(FREE_PROMPT_STATE_KEY, value => this.freePrompt = value);
        state.Restore(IMPORTED_PROMPT_STATE_KEY, value => this.importedPrompt = value);
        state.Restore(PROMPT_FILE_PATH_STATE_KEY, value => this.promptFilePath = value);
        state.Restore(PROMPT_FILE_LOAD_ISSUE_STATE_KEY, value => this.promptFileLoadIssue = value);
        state.Restore(SELECTED_POLICY_STATE_KEY, value => this.selectedPolicy = value);
        state.Restore(OUTPUT_MODE_STATE_KEY, value => this.outputMode = value);
        state.Restore(RESULT_COLUMN_HEADER_STATE_KEY, value => this.resultColumnHeader = value);
        state.Restore(CSV_FILE_NAME_STATE_KEY, value => this.csvFileName = value);
        state.Restore(CSV_SEPARATOR_STATE_KEY, value => this.csvSeparator = value);
        state.Restore(CUSTOM_CSV_SEPARATOR_STATE_KEY, value => this.customCsvSeparator = value);
        state.Restore(FILE_RESULTS_STATE_KEY, values =>
        {
            this.fileResults.Clear();
            this.fileResults.AddRange(values.Select(CloneFileResult));
        });
        state.RestoreHashSet(USED_RESULT_FILE_NAMES_STATE_KEY, this.usedResultFileNames);
        state.Restore(IS_PROCESSING_BATCH_STATE_KEY, value => this.isProcessingBatch = value);
        state.Restore(HAS_REPORTED_WRITE_FAILURE_STATE_KEY, value => this.hasReportedWriteFailure = value);
        state.Restore(NUM_PROCESSED_FILES_STATE_KEY, value => this.numProcessedFiles = value);
    }

    private static BatchProcessingFileResult CloneFileResult(BatchProcessingFileResult source)
    {
        return new()
        {
            FilePath = source.FilePath,
            FileName = source.FileName,
            RelativePath = source.RelativePath,
            Status = source.Status,
            Message = source.Message,
            ResultText = source.ResultText,
            ModelName = source.ModelName,
            ProcessedAt = source.ProcessedAt,
        };
    }
}