using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalFileInfoDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public DataSourceLocalFile DataSource { get; set; }
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.embeddingProvider = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.DataSource.EmbeddingId);
        this.fileInfo = new FileInfo(this.DataSource.FilePath);
        await base.OnInitializedAsync();
    }

    #endregion

    private EmbeddingProvider embeddingProvider;
    private FileInfo fileInfo = null!;
    
    private bool IsCloudEmbedding => !this.embeddingProvider.IsSelfHosted;

    private bool IsFileAvailable => this.fileInfo.Exists;
    
    private string FileSize => this.fileInfo.FileSize();
    
    private async Task CopyToClipboard(string content) => await this.RustService.CopyText2Clipboard(this.Snackbar, content);

    private void Close() => this.MudDialog.Close();
}