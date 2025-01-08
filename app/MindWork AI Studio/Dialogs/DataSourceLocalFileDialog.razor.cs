using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalFileDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceLocalFile DataSource { get; set; }
    
    [Parameter]
    public IReadOnlyList<ConfigurationSelectData<string>> AvailableEmbeddings { get; set; } = [];
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
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
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
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
    
    private string? ValidateName(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
            return "The name must not be empty.";
        
        if (value.Length > 40)
            return "The name must not exceed 40 characters.";
        
        var lowerName = value.ToLowerInvariant();
        if(lowerName != this.dataEditingPreviousInstanceName && this.UsedDataSourcesNames.Contains(lowerName))
            return "The name is already used by another data source. Please choose a different name.";
        
        return null;
    }
    
    private string? ValidateFilePath(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
            return "The file path must not be empty. Please select a file.";
        
        return null;
    }
    
    private string? ValidateEmbeddingId(string value)
    {
        if(string.IsNullOrWhiteSpace(value))
            return "Please select an embedding provider.";
        
        return null;
    }
    
    private string? ValidateUserAcknowledgedCloudEmbedding(bool value)
    {
        if(this.SelectedCloudEmbedding && !value)
            return "Please acknowledge that you are aware of the cloud embedding implications.";
        
        return null;
    }
    
    private bool SelectedCloudEmbedding => !this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.dataEmbeddingId).IsSelfHosted;

    private DataSourceLocalFile CreateDataSource() => new()
    {
        Id = this.dataId,
        Num = this.dataNum,
        Name = this.dataName,
        Type = DataSourceType.LOCAL_FILE,
        EmbeddingId = this.dataEmbeddingId,
        FilePath = this.dataFilePath,
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