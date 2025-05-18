using System.Text;

using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalDirectoryInfoDialog : MSGComponentBase, IAsyncDisposable
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public DataSourceLocalDirectory DataSource { get; set; }

    private readonly Timer refreshTimer = new(TimeSpan.FromSeconds(1.6))
    {
        AutoReset = true,
    };
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.embeddingProvider = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.DataSource.EmbeddingId);
        this.directoryInfo = new DirectoryInfo(this.DataSource.Path);
        
        if (this.directoryInfo.Exists)
        {
            this.directorySizeTask = this.directoryInfo.DetermineContentSize(this.UpdateDirectorySize, this.UpdateDirectoryFiles, this.UpdateFileList, MAX_FILES_TO_SHOW, this.DirectoryOperationDone, this.cts.Token);
            this.refreshTimer.Elapsed += (_, _) => this.InvokeAsync(this.StateHasChanged);
            this.refreshTimer.Start();
        }

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
    }

    private void UpdateDirectorySize(long size)
    {
        this.directorySizeBytes = size;
    }

    private void UpdateDirectoryFiles(long numFiles) => this.directorySizeNumFiles = numFiles;

    private void DirectoryOperationDone()
    {
        this.refreshTimer.Stop();
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
            this.refreshTimer.Stop();
            this.refreshTimer.Dispose();
        }
        catch
        {
            // ignored
        }
    }

    #endregion
}