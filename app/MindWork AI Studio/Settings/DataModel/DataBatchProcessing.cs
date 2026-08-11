using System.Linq.Expressions;

using AIStudio.Assistants.BatchProcessing;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores managed defaults for the Batch Processing Assistant.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataBatchProcessing(Expression<Func<Data, DataBatchProcessing>>? configSelection = null)
{
    public const string DEFAULT_FILE_PATTERNS = "*.pdf;*.docx;*.pptx;*.xlsx;*.md;*.txt;*.mp3;*.wav;*.wave;*.aac;*.flac;*.ogg;*.opus;*.m4a;*.m4b;*.wma;*.alac;*.aif;*.aiff;*.caf;*.mp4;*.m4v;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm";

    /// <summary>
    /// Initializes an unmanaged Batch Processing settings instance.
    /// </summary>
    public DataBatchProcessing() : this(null)
    {
    }

    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectOptions, false);

    public string InputDirectory { get; set; } = ManagedConfiguration.Register(configSelection, value => value.InputDirectory, string.Empty);

    public string OutputDirectory { get; set; } = ManagedConfiguration.Register(configSelection, value => value.OutputDirectory, string.Empty);

    public string FilePatterns { get; set; } = ManagedConfiguration.Register(configSelection, value => value.FilePatterns, DEFAULT_FILE_PATTERNS);

    public bool IncludeSubdirectories { get; set; } = ManagedConfiguration.Register(configSelection, value => value.IncludeSubdirectories, false);

    public BatchProcessingPromptSource PromptSource { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PromptSource, BatchProcessingPromptSource.FREE_PROMPT);

    public string FreePrompt { get; set; } = ManagedConfiguration.Register(configSelection, value => value.FreePrompt, string.Empty);

    public string PromptFilePath { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PromptFilePath, string.Empty);

    public string PreselectedPolicyId { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedPolicyId, string.Empty);

    public BatchProcessingOutputMode OutputMode { get; set; } = ManagedConfiguration.Register(configSelection, value => value.OutputMode, BatchProcessingOutputMode.MARKDOWN_FILES);

    public string CsvFileName { get; set; } = ManagedConfiguration.Register(configSelection, value => value.CsvFileName, string.Empty);

    public string ResultColumnHeader { get; set; } = ManagedConfiguration.Register(configSelection, value => value.ResultColumnHeader, string.Empty);

    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, value => value.MinimumProviderConfidence, ConfidenceLevel.NONE);

    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, string.Empty);
}