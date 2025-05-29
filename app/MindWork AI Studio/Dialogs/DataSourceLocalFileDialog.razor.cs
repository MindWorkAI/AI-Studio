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
    private bool dataUserAcknowledgedCloudEmbedding;
    private string dataEmbeddingId = string.Empty;
    private string dataFilePath = string.Empty;
    private ushort dataMaxMatches = 10;
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
            this.dataEmbeddingId = this.DataSource.EmbeddingId;
            this.dataFilePath = this.DataSource.FilePath;
            this.dataSecurityPolicy = this.DataSource.SecurityPolicy;
            this.dataMaxMatches = this.DataSource.MaxMatches;
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
    
    private bool SelectedCloudEmbedding => !this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.dataEmbeddingId).IsSelfHosted;

    private DataSourceLocalFile CreateDataSource() => new()
    {
        Id = this.dataId,
        Num = this.dataNum,
        Name = this.dataName,
        Type = DataSourceType.LOCAL_FILE,
        EmbeddingId = this.dataEmbeddingId,
        FilePath = this.dataFilePath,
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
}