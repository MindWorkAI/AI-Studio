using System.Text;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalDirectoryInfoDialog : ComponentBase, IAsyncDisposable
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public DataSourceLocalDirectory DataSource { get; set; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.embeddingProvider = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.DataSource.EmbeddingId);
        this.directoryInfo = new DirectoryInfo(this.DataSource.Path);
        
        if (this.directoryInfo.Exists)
            this.directorySizeTask = this.directoryInfo.DetermineContentSize(this.UpdateDirectorySize, this.UpdateDirectoryFiles, this.UpdateFileList, MAX_FILES_TO_SHOW, this.DirectoryOperationDone, this.cts.Token);
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private const int MAX_FILES_TO_SHOW = 100;
    
    private readonly CancellationTokenSource cts = new();
    
    private EmbeddingProvider embeddingProvider;
    private DirectoryInfo directoryInfo = null!;
    private long directorySizeBytes;
    private long directorySizeNumFiles;
    private readonly StringBuilder directoryFiles = new();
    private Task directorySizeTask = Task.CompletedTask;
    
    private bool IsOperationInProgress { get; set; } = true;

    private bool IsCloudEmbedding => !this.embeddingProvider.IsSelfHosted;

    private bool IsDirectoryAvailable => this.directoryInfo.Exists;

    private void UpdateFileList(string file)
    {
        this.directoryFiles.Append("- ");
        this.directoryFiles.AppendLine(file);
        this.InvokeAsync(this.StateHasChanged);
    }

    private void UpdateDirectorySize(long size)
    {
        this.directorySizeBytes = size;
        this.InvokeAsync(this.StateHasChanged);
    }

    private void UpdateDirectoryFiles(long numFiles) => this.directorySizeNumFiles = numFiles;

    private void DirectoryOperationDone()
    {
        this.IsOperationInProgress = false;
        this.InvokeAsync(this.StateHasChanged);
    }
    
    private string NumberFilesInDirectory => $"{this.directorySizeNumFiles:###,###,###,###}";

    private void Close()
    {
        this.cts.Cancel();
        this.MudDialog.Close();
    }

    #region Implementation of IDisposable

    public async ValueTask DisposeAsync()
    {
        try
        {
            await this.cts.CancelAsync();
            await this.directorySizeTask;
        
            this.cts.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    #endregion
}