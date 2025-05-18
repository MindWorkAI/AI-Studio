using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalFileInfoDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public DataSourceLocalFile DataSource { get; set; }

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
    
    private void Close() => this.MudDialog.Close();
}