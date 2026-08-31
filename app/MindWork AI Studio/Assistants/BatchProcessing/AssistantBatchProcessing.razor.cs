using AIStudio.Dialogs.Settings;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing : AssistantBaseCore<SettingsDialogBatchProcessing>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PandocAvailabilityService PandocAvailability { get; init; } = null!;

    private const string DEFAULT_OUTPUT_DIRECTORY_NAME = "ai-results";
    private const string DEFAULT_RESULTS_FILENAME = "batch-results.csv";
    private const string CSV_EXTENSION = ".csv";
    private const string RESULT_FILE_SUFFIX = "_result";
    private const string TRANSCRIPT_FILE_SUFFIX = ".transcript.md";
    private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
    private const char LOG_SEPARATOR = ';';

    /// <summary>
    /// The name of the log file. It is fixed, so that a later batch run finds
    /// the log of a previous run and can continue it.
    /// </summary>
    private const string LOG_FILENAME = "log.csv";

    protected override Tools.Components Component => Tools.Components.BATCH_PROCESSING_ASSISTANT;

    protected override string Title => T("Batch Processing Assistant");

    protected override string Description => T("Process all documents and media files of a folder in one batch run: documents are converted to Markdown, while audio and video files are transcribed automatically, before their content is sent to the AI along with your instructions. You choose whether each answer is stored as its own Markdown file or whether all answers are collected in one CSV results table. A log records what happened to every file, so a run which was interrupted or produced errors can be continued later. A single failing file never stops the entire run.");

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

        this.ApplyFormDefaults();
        this.importedPrompt = string.Empty;
        this.promptFileLoadIssue = string.Empty;
        this.fileResults.Clear();
        this.usedResultFileNames.Clear();
        this.hasReportedWriteFailure = false;
        this.numProcessedFiles = 0;
        this.pauseBeforeNextFileSeconds = 0;
    }

    protected override bool MightPreselectValues()
    {
        if (!this.SettingsManager.ConfigurationData.BatchProcessing.PreselectOptions)
            return false;

        this.ApplyFormDefaults();
        return true;
    }

    protected override async Task OnDefaultsAppliedAsync()
    {
        await this.LoadConfiguredPromptFileAsync();
        this.ApplyPolicyPreselection();
    }

    private string inputDirectory = string.Empty;
    private string outputDirectory = string.Empty;
    private string filePatterns = DataBatchProcessing.DEFAULT_FILE_PATTERNS;
    private bool includeSubdirectories;
    private BatchProcessingPromptSource promptSource = BatchProcessingPromptSource.FREE_PROMPT;
    private string freePrompt = string.Empty;
    private string importedPrompt = string.Empty;
    private string promptFilePath = string.Empty;
    private string promptFileLoadIssue = string.Empty;
    private DataDocumentAnalysisPolicy? selectedPolicy;
    private BatchProcessingOutputMode outputMode = BatchProcessingOutputMode.INDIVIDUAL_FILES;
    private FileExportFormat resultFileFormat = FileExportFormat.MARKDOWN;
    private string resultColumnHeader = string.Empty;
    private string csvFileName = string.Empty;
    private BatchProcessingCsvSeparator csvSeparator = BatchProcessingCsvSeparator.SEMICOLON;
    private string customCsvSeparator = string.Empty;
    private int minimumDelaySeconds = DataBatchProcessing.DEFAULT_MIN_DELAY_SECONDS;
    private int maximumDelaySeconds = DataBatchProcessing.DEFAULT_MAX_DELAY_SECONDS;

    private readonly List<BatchProcessingFileResult> fileResults = [];
    private readonly HashSet<string> usedResultFileNames = new(StringComparer.OrdinalIgnoreCase);
    private bool isProcessingBatch;
    private bool hasReportedWriteFailure;
    private int numProcessedFiles;
    private int pauseBeforeNextFileSeconds;

    /// <summary>
    /// The header of the column of the results table that holds the AI answer.
    /// </summary>
    private string ResultColumnHeader => string.IsNullOrWhiteSpace(this.resultColumnHeader) ? T("Result") : this.resultColumnHeader.Trim();

    /// <summary>
    /// Updates the manually imported prompt and stops presenting an obsolete
    /// configured path or load error once the user has selected another file.
    /// </summary>
    private string ImportedPrompt
    {
        get => this.importedPrompt;
        set
        {
            this.importedPrompt = value;
            this.promptFilePath = string.Empty;
            this.promptFileLoadIssue = string.Empty;
        }
    }

    private bool ConfiguredPolicyIsMissing
    {
        get
        {
            var settings = this.SettingsManager.ConfigurationData.BatchProcessing;
            return settings.PreselectOptions
                   && this.promptSource is BatchProcessingPromptSource.POLICY
                   && !string.IsNullOrWhiteSpace(settings.PreselectedPolicyId)
                   && this.selectedPolicy is null;
        }
    }

    private void RestoreDefaultFilePatterns() => this.filePatterns = DataBatchProcessing.DEFAULT_FILE_PATTERNS;

    private ConfidenceLevel GetMinimumConfidenceLevel()
    {
        var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(this.Component);
        if (this.promptSource is BatchProcessingPromptSource.POLICY
            && this.selectedPolicy is not null
            && this.selectedPolicy.MinimumProviderConfidence > minimumLevel)
            minimumLevel = this.selectedPolicy.MinimumProviderConfidence;

        return minimumLevel;
    }

    private void ApplyFormDefaults()
    {
        var settings = this.SettingsManager.ConfigurationData.BatchProcessing;
        if (!settings.PreselectOptions)
        {
            this.inputDirectory = string.Empty;
            this.outputDirectory = string.Empty;
            this.filePatterns = DataBatchProcessing.DEFAULT_FILE_PATTERNS;
            this.includeSubdirectories = false;
            this.promptSource = BatchProcessingPromptSource.FREE_PROMPT;
            this.freePrompt = string.Empty;
            this.promptFilePath = string.Empty;
            this.selectedPolicy = null;
            this.outputMode = BatchProcessingOutputMode.INDIVIDUAL_FILES;
            this.resultFileFormat = FileExportFormat.MARKDOWN;
            this.resultColumnHeader = string.Empty;
            this.csvFileName = string.Empty;
            this.csvSeparator = BatchProcessingCsvSeparator.SEMICOLON;
            this.customCsvSeparator = string.Empty;
            this.minimumDelaySeconds = MinimumDelayIsManaged ? this.ManagedMinimumDelaySeconds : DataBatchProcessing.DEFAULT_MIN_DELAY_SECONDS;
            this.maximumDelaySeconds = Math.Clamp(DataBatchProcessing.DEFAULT_MAX_DELAY_SECONDS, this.minimumDelaySeconds, DataBatchProcessing.MAX_DELAY_SECONDS);
            return;
        }

        this.inputDirectory = settings.InputDirectory;
        this.outputDirectory = settings.OutputDirectory;
        this.filePatterns = settings.FilePatterns;
        this.includeSubdirectories = settings.IncludeSubdirectories;
        this.promptSource = settings.PromptSource;
        this.freePrompt = settings.FreePrompt;
        this.promptFilePath = settings.PromptFilePath;
        this.selectedPolicy = this.SettingsManager.ConfigurationData.DocumentAnalysis.Policies
            .FirstOrDefault(policy => policy.Id == settings.PreselectedPolicyId);
        this.outputMode = settings.OutputMode;
        this.resultFileFormat = settings.ResultFileFormat;
        this.resultColumnHeader = settings.ResultColumnHeader;
        this.csvFileName = settings.CsvFileName;
        this.csvSeparator = settings.CsvSeparator;
        this.customCsvSeparator = settings.CustomCsvSeparator;
        this.minimumDelaySeconds = MinimumDelayIsManaged ? this.ManagedMinimumDelaySeconds
            : Math.Clamp(settings.MinimumDelaySeconds, DataBatchProcessing.MIN_DELAY_SECONDS, DataBatchProcessing.MAX_DELAY_SECONDS);
        this.maximumDelaySeconds = Math.Clamp(settings.MaximumDelaySeconds, this.minimumDelaySeconds, DataBatchProcessing.MAX_DELAY_SECONDS);
    }

    private async Task LoadConfiguredPromptFileAsync()
    {
        this.promptFileLoadIssue = string.Empty;
        if (this.promptSource is not BatchProcessingPromptSource.FILE_IMPORT || string.IsNullOrWhiteSpace(this.promptFilePath))
            return;

        this.importedPrompt = string.Empty;
        if (!string.Equals(Path.GetExtension(this.promptFilePath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            this.promptFileLoadIssue = T("The configured instructions file must be a Markdown file (*.md).");
            return;
        }

        if (!File.Exists(this.promptFilePath))
        {
            this.promptFileLoadIssue = T("The configured instructions file no longer exists.");
            return;
        }

        try
        {
            this.importedPrompt = await File.ReadAllTextAsync(this.promptFilePath);
            if (string.IsNullOrWhiteSpace(this.importedPrompt))
                this.promptFileLoadIssue = T("The configured instructions file is empty.");
        }
        catch (Exception exception)
        {
            this.Logger.LogError(exception, "Could not load the configured batch instructions file '{PromptFilePath}'.", this.promptFilePath);
            this.promptFileLoadIssue = T("The configured instructions file could not be read.");
        }
    }

    private void PromptSourceChanged(BatchProcessingPromptSource source)
    {
        this.promptSource = source;
        if (source is BatchProcessingPromptSource.POLICY)
            this.ApplyPolicyPreselection();
        else
            this.ResetProviderAndProfileSelection();
    }

    private void SelectedPolicyChanged(DataDocumentAnalysisPolicy? policy)
    {
        this.selectedPolicy = policy;
        this.ApplyPolicyPreselection();
    }

    private void ApplyPolicyPreselection()
    {
        if (this.promptSource is not BatchProcessingPromptSource.POLICY || this.selectedPolicy is null)
            return;

        var minimumLevel = this.GetMinimumConfidenceLevel();
        var policyProvider = this.SettingsManager.GetPreselectedProvider(this.Component, this.selectedPolicy.PreselectedProvider);
        if (policyProvider != Settings.Provider.NONE
            && policyProvider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
            this.ProviderSettings = policyProvider;
        else
        {
            var fallbackProvider = this.SettingsManager.GetPreselectedProvider(this.Component, usePreselectionBeforeCurrentProvider: true);
            this.ProviderSettings = fallbackProvider != Settings.Provider.NONE
                                    && fallbackProvider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel
                ? fallbackProvider
                : Settings.Provider.NONE;
        }
    }
}
