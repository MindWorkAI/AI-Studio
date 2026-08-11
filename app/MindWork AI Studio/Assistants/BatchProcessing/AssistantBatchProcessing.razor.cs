using AIStudio.Dialogs.Settings;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing : AssistantBaseCore<NoSettingsPanel>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private const string DEFAULT_FILE_PATTERNS = "*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt";
    private const string DEFAULT_OUTPUT_DIRECTORY_NAME = "ai-results";
    private const string DEFAULT_RESULTS_FILENAME = "batch-results.csv";
    private const string CSV_EXTENSION = ".csv";
    private const string RESULT_FILE_SUFFIX = "_result.md";
    private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// The name of the log file. It is fixed, so that a later batch run finds
    /// the log of a previous run and can continue it.
    /// </summary>
    private const string LOG_FILENAME = "log.csv";

    protected override Tools.Components Component => Tools.Components.BATCH_PROCESSING_ASSISTANT;

    protected override string Title => T("Batch Processing Assistant");

    protected override string Description => T("Process all documents of a folder in one batch run: each document is converted to Markdown and sent to the AI along with your instructions. You choose whether each answer is stored as its own Markdown file or whether all answers are collected in one CSV results table. A log records what happened to every document, so a run which was interrupted or produced errors can be continued later. A single failing document never stops the entire run.");

    protected override string SystemPrompt => this.BuildSystemPrompt();

    protected override string SubmitText => T("Start batch processing");

    protected override Func<Task> SubmitAction => this.StartBatchProcessingAsync;

    protected override bool SubmitDisabled => this.isProcessingBatch;

    protected override bool ShowResult => false;

    protected override bool AllowProfiles => false;

    protected override bool ShowSendTo => false;

    protected override bool ShowCopyResult => false;

    protected override void ResetForm()
    {
        if (this.isProcessingBatch)
            return;

        this.inputDirectory = string.Empty;
        this.outputDirectory = string.Empty;
        this.filePatterns = DEFAULT_FILE_PATTERNS;
        this.includeSubdirectories = false;
        this.promptSource = BatchProcessingPromptSource.FREE_PROMPT;
        this.freePrompt = string.Empty;
        this.importedPrompt = string.Empty;
        this.selectedPolicy = null;
        this.outputMode = BatchProcessingOutputMode.MARKDOWN_FILES;
        this.resultColumnHeader = string.Empty;
        this.csvFileName = string.Empty;
        this.fileResults.Clear();
        this.usedResultFileNames.Clear();
        this.numProcessedFiles = 0;
    }

    protected override bool MightPreselectValues() => false;

    private string inputDirectory = string.Empty;
    private string outputDirectory = string.Empty;
    private string filePatterns = DEFAULT_FILE_PATTERNS;
    private bool includeSubdirectories;
    private BatchProcessingPromptSource promptSource = BatchProcessingPromptSource.FREE_PROMPT;
    private string freePrompt = string.Empty;
    private string importedPrompt = string.Empty;
    private DataDocumentAnalysisPolicy? selectedPolicy;
    private BatchProcessingOutputMode outputMode = BatchProcessingOutputMode.MARKDOWN_FILES;
    private string resultColumnHeader = string.Empty;
    private string csvFileName = string.Empty;

    private readonly List<BatchProcessingFileResult> fileResults = [];
    private readonly HashSet<string> usedResultFileNames = new(StringComparer.OrdinalIgnoreCase);
    private bool isProcessingBatch;
    private bool hasReportedWriteFailure;
    private int numProcessedFiles;

    /// <summary>
    /// The header of the column of the results table that holds the AI answer.
    /// </summary>
    private string ResultColumnHeader => string.IsNullOrWhiteSpace(this.resultColumnHeader) ? T("Result") : this.resultColumnHeader.Trim();

    private ConfidenceLevel GetMinimumConfidenceLevel()
    {
        if (this.promptSource is BatchProcessingPromptSource.POLICY && this.selectedPolicy is not null)
            return this.selectedPolicy.MinimumProviderConfidence;

        return ConfidenceLevel.NONE;
    }
}