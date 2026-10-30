using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

using MudBlazor.Interfaces;
using MudBlazor.Utilities;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalDirectoryDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceLocalDirectory DataSource { get; set; }

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    private readonly DataSourceValidation dataSourceValidation;
    
    /// <summary>
    /// The list of used data source names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedDataSourcesNames { get; set; } = [];

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousInstanceName = string.Empty;
    
    private uint dataNum;
    private string dataId = Guid.NewGuid().ToString();
    private string dataName = string.Empty;
    private string dataDescription = string.Empty;
    private CloudProcessing acknowledgedCloudProcessing;
    private string dataEmbeddingId = string.Empty;
    private string dataPath = string.Empty;
    private string dataFilePatterns = DataSourceLocalDirectory.DEFAULT_FILE_PATTERNS;
    private string dataExcludePatterns = FileExclusions.DEFAULT_PATTERNS;
    private int dataMaxFileSizeMegabytes = FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES;
    private bool dataIncludeSubdirectories = DataSourceLocalDirectory.DEFAULT_INCLUDE_SUBDIRECTORIES;
    private bool dataIncludeHiddenFilesAndFolders = DataSourceLocalDirectory.DEFAULT_INCLUDE_HIDDEN_FILES_AND_FOLDERS;
    private int dataMaxChunkTokenLength;
    private int dataChunkOverlapTokenLength = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
    private ushort dataMaxMatches = 10;
    private bool showExpertSettings;
    private bool revalidateAfterRender;
    private ConfidenceLevel dataConfidenceLevel = ConfidenceLevel.UNKNOWN;
    private OutboundDataRestriction dataOutboundDataRestriction = DataSourceOutboundDataExtensions.DEFAULT_OUTBOUND_DATA_RESTRICTION;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    // The fields whose rules read other fields, see RevalidateDependentFields:
    private MudSelect<string> embeddingSelect = null!;
    private MudSelect<ConfidenceLevel> confidenceLevelSelect = null!;
    private MudNumericField<int> maxChunkTokenLengthField = null!;
    private MudNumericField<int> chunkOverlapTokenLengthField = null!;

    public DataSourceLocalDirectoryDialog()
    {
        this.dataSourceValidation = new()
        {
            GetSelectedCloudEmbedding = () => this.SelectedCloudEmbedding,
            GetSelectedCloudTranscription = () => this.SelectedCloudTranscription,
            GetSelectedEmbeddingProvider = () => this.SelectedEmbedding,
            GetConfidenceLevel = () => this.dataConfidenceLevel,
            GetSettingsManager = () => this.SettingsManager,
            GetPreviousDataSourceName = () => this.dataEditingPreviousInstanceName,
            GetUsedDataSourceNames = () => this.UsedDataSourcesNames,
        };
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names, those of the mailboxes included:
        this.UsedDataSourcesNames = this.SettingsManager.ConfigurationData.DataSources.Select(x => x.Name.ToLowerInvariant())
            .Concat(this.SettingsManager.ConfigurationData.Mailboxes.Select(x => x.Name.ToLowerInvariant()))
            .ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataSource.Name.ToLowerInvariant();
            this.dataNum = this.DataSource.Num;
            this.dataId = this.DataSource.Id;
            this.dataName = this.DataSource.Name;
            this.dataDescription = this.DataSource.Description;
            this.dataEmbeddingId = this.DataSource.EmbeddingId;
            this.dataPath = this.DataSource.Path;
            this.dataFilePatterns = this.DataSource.EffectiveFilePatterns;
            this.dataExcludePatterns = this.DataSource.EffectiveExcludePatterns;
            this.dataMaxFileSizeMegabytes = this.DataSource.EffectiveMaxFileSizeMegabytes;
            this.dataIncludeSubdirectories = this.DataSource.IncludeSubdirectories;
            this.dataIncludeHiddenFilesAndFolders = this.DataSource.IncludeHiddenFilesAndFolders;
            this.dataMaxChunkTokenLength = this.DataSource.MaxChunkTokenLength;
            this.dataChunkOverlapTokenLength = this.DataSource.ChunkOverlapTokenLength;
            this.dataConfidenceLevel = this.DataSource.ConfidenceLevel;
            this.dataOutboundDataRestriction = this.DataSource.OutboundDataRestriction;
            this.dataMaxMatches = this.DataSource.MaxMatches;
        }
        
        await base.OnInitializedAsync();

        // The embedding providers may change while the dialog is open, e.g., through a configuration plugin:
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            await this.form.ResetValidationAsync();

        // A check asked for in code waits until the fields hold their new values, cf. ToggleExpertSettings:
        if (this.revalidateAfterRender)
        {
            this.revalidateAfterRender = false;
            await this.RevalidateDependentFields(changedField: null);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
            case Event.PLUGINS_RELOADED:
                // The embedding providers are listed anew, and the fields checked so far get a fresh verdict once they hold the new values:
                this.revalidateAfterRender = true;
                this.StateHasChanged();
                break;
        }

        return base.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
    }

    #endregion

    private EmbeddingProvider? SelectedEmbedding => this.SettingsManager.ConfigurationData.EmbeddingProviders
        .FirstOrDefault(x => x.Id == this.dataEmbeddingId);

    private bool SelectedCloudEmbedding => this.SelectedEmbedding is { IsSelfHosted: false };

    /// <summary>
    /// Whether the file patterns may select audio or video files, which the data source transcribes.
    /// </summary>
    private bool SelectsMedia => FileNamePatterns.MaySelectMedia(FileNamePatterns.Parse(this.dataFilePatterns));

    /// <summary>
    /// The transcription provider which may hear the audio and video files of this data source, or null.
    /// </summary>
    private TranscriptionProvider? AllowedTranscriptionProvider => this.MediaTranscriptionService.GetUsableProvider(this.CreateDataSource());

    private bool SelectedCloudTranscription => this.SelectsMedia && this.AllowedTranscriptionProvider is { IsSelfHosted: false };

    private string CloudTranscriptionProviderName => this.SelectedCloudTranscription ? this.AllowedTranscriptionProvider?.Name ?? string.Empty : string.Empty;

    private CloudProcessing CurrentCloudProcessing => (this.SelectedCloudEmbedding ? CloudProcessing.EMBEDDING : CloudProcessing.NONE) | (this.SelectedCloudTranscription ? CloudProcessing.TRANSCRIPTION : CloudProcessing.NONE);

    /// <remarks>
    /// The acknowledgment holds for the providers it was given for, cf. CloudProcessingExtensions.Covers.
    /// </remarks>
    private bool UserAcknowledgedCloudProcessing
    {
        get => this.acknowledgedCloudProcessing.Covers(this.CurrentCloudProcessing);
        set => this.acknowledgedCloudProcessing = value ? this.CurrentCloudProcessing : CloudProcessing.NONE;
    }

    /// <summary>
    /// Why audio and video files which the patterns select would not be indexed, or null when they would.
    /// </summary>
    private string? MediaTranscriptionHint => !this.SelectsMedia
        ? null
        : this.MediaTranscriptionService.GetUsableProvider() is null
            ? T("Audio and video files which this data source selects are skipped until you choose a transcription provider in the settings.")
            : this.AllowedTranscriptionProvider is null
                ? T("Audio and video files which this data source selects are skipped, because your transcription provider does not meet the confidence level this data source requires.")
                : null;

    private IEnumerable<ConfigurationSelectData<ConfidenceLevel>> ConfidenceLevels => ConfigurationSelectDataFactory.GetDataSourceConfidenceLevelsData();

    private OutboundDataRestriction OrganizationMinimumOutboundDataRestriction => this.SettingsManager.ConfigurationData.DataSourceSecurity.MinimumOutboundDataRestriction;

    /// <summary>
    /// The level the data source gets: the one chosen, as far as the organization allows it.
    /// </summary>
    /// <remarks>
    /// The organization may rule out a level while the dialog is open, or after the data source was
    /// stored with it. The choice itself stays, so the dialog shows it again once the organization
    /// allows it again.
    /// </remarks>
    private OutboundDataRestriction SelectedOutboundDataRestriction
    {
        get => this.dataOutboundDataRestriction.StricterOf(this.OrganizationMinimumOutboundDataRestriction);
        set => this.dataOutboundDataRestriction = value;
    }

    private string SelectedEmbeddingTokenizerText => this.SelectedEmbedding is null
        ? T("No embedding selected")
        : string.IsNullOrWhiteSpace(this.SelectedEmbedding.TokenizerPath)
            ? T("Default tokenizer")
            : Path.GetFileName(this.SelectedEmbedding.TokenizerPath);

    private int ProviderMaxChunkTokenLength => this.SelectedEmbedding?.EffectiveTokenLimit ?? EmbeddingProvider.DEFAULT_TOKEN_LIMIT;

    private string MaxChunkTokenLengthHelperText => string.Format(
        T("Maximum number of tokens per chunk for this data source. The embedding provider default is {0} tokens."),
        this.ProviderMaxChunkTokenLength);

    private string ChunkOverlapTokenLengthHelperText => string.Format(
        T("Number of tokens repeated at the start of the next chunk. The default overlap is {0} tokens."),
        DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH);

    private DataSourceLocalDirectory CreateDataSource() => new()
    {
        Id = this.dataId,
        Num = this.dataNum,
        Name = this.dataName,
        Description = this.dataDescription,
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = this.dataEmbeddingId,

        Path = this.dataPath,
        FilePatterns = this.NormalizeFilePatterns(),

        // Unlike the file patterns, no normalizing, since the exclusion editor passes on valid lists only:
        ExcludePatterns = this.dataExcludePatterns,
        MaxFileSizeMegabytes = this.dataMaxFileSizeMegabytes,
        IncludeSubdirectories = this.dataIncludeSubdirectories,
        IncludeHiddenFilesAndFolders = this.dataIncludeHiddenFilesAndFolders,
        MaxChunkTokenLength = this.dataMaxChunkTokenLength,
        ChunkOverlapTokenLength = this.dataChunkOverlapTokenLength,
        ConfidenceLevel = this.dataConfidenceLevel,
        OutboundDataRestriction = this.SelectedOutboundDataRestriction,
        MaxMatches = this.dataMaxMatches,
    };

    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        await this.StoreValidatedDataSourceAsync();
    }

    /// <summary>
    /// Continues Save after form validation, asking before the draft is returned to its caller.
    /// </summary>
    internal async Task StoreValidatedDataSourceAsync()
    {
        var addedDataSource = this.CreateDataSource();

        //
        // Ask while the dialog is still open, so a token limit which would have cost the prepared
        // documents can be corrected right away. Asking in DataSourceManagement instead would have
        // to be written once per data source kind, and by then the numbers are out of reach.
        //
        // Only when editing: while adding, DataSource is still default -- both local data sources
        // are record structs -- and nothing has been prepared for a source which does not exist yet.
        //
        if (this.IsEditing && !await DataSourceReindexWarning.ConfirmDataSourceChangeAsync(this.DialogService, this.SettingsManager, this.DataSourceEmbeddingService, this.DataSource, addedDataSource))
            return;

        this.MudDialog.Close(DialogResult.Ok(addedDataSource));
    }
    
    private void Cancel() => this.MudDialog.Cancel();

    private string NormalizeFilePatterns() => FileNamePatterns.Normalize(this.dataFilePatterns);

    private void RestoreDefaultFilePatterns() => this.dataFilePatterns = DataSourceLocalDirectory.DEFAULT_FILE_PATTERNS;

    /// <summary>
    /// Gives the fields which are checked against each other a fresh verdict: the embedding provider
    /// and the required confidence level, and the token limits, which depend on the embedding provider.
    /// </summary>
    private Task RevalidateDependentFields(IFormComponent? changedField) => DependentFieldValidation.RevalidateAsync(changedField, this.embeddingSelect, this.confidenceLevelSelect, this.maxChunkTokenLengthField, this.chunkOverlapTokenLengthField);

    private Task RevalidateAfterFieldChange(FormFieldChangedEventArgs change) => this.RevalidateDependentFields(change.Field);

    private string? ValidateMaxChunkTokenLength(int maxChunkTokenLength)
    {
        if (!this.showExpertSettings)
            return null;

        if (maxChunkTokenLength < 1)
            return T("Please enter a token limit of at least 1.");

        var providerMaxChunkTokenLength = this.ProviderMaxChunkTokenLength;
        if (maxChunkTokenLength > providerMaxChunkTokenLength)
            return string.Format(T("The data source token limit must not be larger than the embedding provider token limit ({0})."), providerMaxChunkTokenLength);

        return null;
    }

    private string? ValidateChunkOverlapTokenLength(int chunkOverlapTokenLength)
    {
        if (!this.showExpertSettings)
            return null;

        if (chunkOverlapTokenLength < 0)
            return T("Please enter 0 or a positive overlap length.");

        var effectiveMaxChunkTokenLength = this.showExpertSettings && this.dataMaxChunkTokenLength > 0
            ? this.dataMaxChunkTokenLength
            : this.ProviderMaxChunkTokenLength;
        if (chunkOverlapTokenLength >= effectiveMaxChunkTokenLength)
            return T("The overlap must be smaller than the effective token limit.");

        return null;
    }

    private void ToggleExpertSettings()
    {
        this.showExpertSettings = !this.showExpertSettings;
        if (this.showExpertSettings && this.dataMaxChunkTokenLength < 1)
            this.dataMaxChunkTokenLength = this.ProviderMaxChunkTokenLength;

        // The token limits are only checked while they are shown. The field learns the limit set
        // above only with the next render, so it is checked after that:
        this.revalidateAfterRender = true;
    }

    /// <summary>
    /// Selects the embedding provider the user just added from within this dialog, so that what they
    /// entered so far can be saved right away.
    /// </summary>
    /// <remarks>
    /// Only when no existing provider is selected, which includes the provider of a data source that
    /// was deleted since. Like the token limit in ToggleExpertSettings, the field learns its new value
    /// only with the next render, so it is checked after that.
    /// </remarks>
    private void SelectAddedEmbeddingProvider(EmbeddingProvider provider)
    {
        if (this.SelectedEmbedding is null)
            this.dataEmbeddingId = provider.Id;

        this.revalidateAfterRender = true;
    }

    private string GetExpertStyles => this.showExpertSettings ? "border-2 border-dashed rounded pa-2" : string.Empty;
}
