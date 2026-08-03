using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalFileDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceLocalFile DataSource { get; set; }

    [Parameter]
    public bool LockSourceAndEmbedding { get; set; }
    
    [Parameter]
    public IReadOnlyList<ConfigurationSelectData<string>> AvailableEmbeddings { get; set; } = [];
    
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
    private bool dataUserAcknowledgedCloudEmbedding;
    private string dataEmbeddingId = string.Empty;
    private string dataFilePath = string.Empty;
    private int dataMaxChunkTokenLength;
    private int dataChunkOverlapTokenLength;
    private ushort dataMaxMatches = 10;
    private bool showExpertSettings;
    private DataSourceSecurity dataSecurityPolicy;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    public DataSourceLocalFileDialog()
    {
        this.dataSourceValidation = new()
        {
            GetSelectedCloudEmbedding = () => this.SelectedCloudEmbedding,
            GetPreviousDataSourceName = () => this.dataEditingPreviousInstanceName,
            GetUsedDataSourceNames = () => this.UsedDataSourcesNames,
        };
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedDataSourcesNames = this.SettingsManager.ConfigurationData.DataSources.Select(x => x.Name.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataSource.Name.ToLowerInvariant();
            this.dataNum = this.DataSource.Num;
            this.dataId = this.DataSource.Id;
            this.dataName = this.DataSource.Name;
            this.dataDescription = this.DataSource.Description;
            this.dataEmbeddingId = this.DataSource.EmbeddingId;
            this.dataFilePath = this.DataSource.FilePath;
            this.dataMaxChunkTokenLength = this.DataSource.MaxChunkTokenLength;
            this.dataChunkOverlapTokenLength = this.DataSource.ChunkOverlapTokenLength;
            this.dataSecurityPolicy = this.DataSource.SecurityPolicy;
            this.dataMaxMatches = this.DataSource.MaxMatches;
            this.showExpertSettings = this.dataMaxChunkTokenLength > 0 || this.dataChunkOverlapTokenLength > 0;
        }
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    private EmbeddingProvider? SelectedEmbedding => this.SettingsManager.ConfigurationData.EmbeddingProviders
        .FirstOrDefault(x => x.Id == this.dataEmbeddingId);

    private bool SelectedCloudEmbedding => this.SelectedEmbedding is { IsSelfHosted: false };

    private bool CanChangeSourceAndEmbedding => !this.IsEditing || !this.LockSourceAndEmbedding;

    private string SelectedEmbeddingTokenizerText => this.SelectedEmbedding is null
        ? T("No embedding selected")
        : string.IsNullOrWhiteSpace(this.SelectedEmbedding.TokenizerPath)
            ? T("Default tokenizer")
            : System.IO.Path.GetFileName(this.SelectedEmbedding.TokenizerPath);

    private DataSourceLocalFile CreateDataSource() => new()
    {
        Id = this.dataId,
        Num = this.dataNum,
        Name = this.dataName,
        Description = this.dataDescription,
        Type = DataSourceType.LOCAL_FILE,
        EmbeddingId = this.CanChangeSourceAndEmbedding ? this.dataEmbeddingId : this.DataSource.EmbeddingId,
        FilePath = this.CanChangeSourceAndEmbedding ? this.dataFilePath : this.DataSource.FilePath,
        MaxChunkTokenLength = this.dataMaxChunkTokenLength,
        ChunkOverlapTokenLength = this.dataChunkOverlapTokenLength,
        SecurityPolicy = this.dataSecurityPolicy,
        MaxMatches = this.dataMaxMatches,
    };
    
    private async Task Store()
    {
        await this.form.Validate();
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        var addedDataSource = this.CreateDataSource();
        this.MudDialog.Close(DialogResult.Ok(addedDataSource));
    }
    
    private void Cancel() => this.MudDialog.Cancel();

    private string? ValidateMaxChunkTokenLength(int maxChunkTokenLength)
    {
        if (maxChunkTokenLength < 0)
            return T("Please enter 0 or a positive token limit.");

        var providerMaxChunkTokenLength = this.SelectedEmbedding?.EffectiveTokenLimit ?? EmbeddingProvider.DEFAULT_TOKEN_LIMIT;
        if (maxChunkTokenLength > 0 && maxChunkTokenLength >= providerMaxChunkTokenLength)
            return string.Format(T("The data source token limit must be smaller than the embedding provider token limit ({0}). Use 0 to use the provider setting."), providerMaxChunkTokenLength);

        return null;
    }

    private string? ValidateChunkOverlapTokenLength(int chunkOverlapTokenLength)
    {
        if (chunkOverlapTokenLength < 0)
            return T("Please enter 0 or a positive overlap length.");

        var effectiveMaxChunkTokenLength = this.dataMaxChunkTokenLength > 0
            ? this.dataMaxChunkTokenLength
            : this.SelectedEmbedding?.EffectiveTokenLimit ?? EmbeddingProvider.DEFAULT_TOKEN_LIMIT;
        if (chunkOverlapTokenLength >= effectiveMaxChunkTokenLength)
            return T("The overlap must be smaller than the effective token limit.");

        return null;
    }

    private void ToggleExpertSettings() => this.showExpertSettings = !this.showExpertSettings;

    private string GetExpertStyles => this.showExpertSettings ? "border-2 border-dashed rounded pa-2" : string.Empty;
}
