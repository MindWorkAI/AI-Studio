using System.Text;

using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using Timer = System.Timers.Timer;

namespace AIStudio.Dialogs;

public partial class DataSourceLocalDirectoryInfoDialog : MSGComponentBase
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
        this.embeddingProvider = this.SettingsManager.ConfigurationData.EmbeddingProviders.FirstOrDefault(x => x.Id == this.DataSource.EmbeddingId) ?? EmbeddingProvider.NONE;
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
    
    private EmbeddingProvider embeddingProvider = EmbeddingProvider.NONE;
    private DirectoryInfo directoryInfo = null!;
    private long directorySizeBytes;
    private long directorySizeNumFiles;
    private readonly StringBuilder directoryFiles = new();
    private string directoryFilesText = string.Empty;
    private Task directorySizeTask = Task.CompletedTask;
    
    private bool IsOperationInProgress { get; set; } = true;

    private bool IsCloudEmbedding => !this.embeddingProvider.IsTrustedForDataSourceSecurityChecks(this.SettingsManager);

    private bool IsDirectoryAvailable => this.directoryInfo.Exists;

    /// <summary>
    /// Takes the next file which the directory scan found.
    /// </summary>
    /// <remarks>
    /// This runs on the scan's own thread, and it does so deliberately, although the scan asks its
    /// callers to reach for a dispatcher. That request is about updating the UI, and none of these
    /// callbacks does: they write fields and nothing else.<br/><br/>
    /// Why that holds is worth writing down, because the code does not show it. The string builder
    /// has exactly one writer -- this method, on that one thread -- and nobody else ever reads it.
    /// What the renderer reads is the text field beside it, and assigning a string reference is
    /// atomic, so a render sees the whole previous text or the whole new one, never half of either.
    /// Building that text anew costs little, because the scan stops reporting files once it has
    /// reported a hundred. And a render happens only when the refresh timer ticks, which goes
    /// through the dispatcher, so a reading taken a moment too early is replaced 1.6 seconds later
    /// anyway.
    /// </remarks>
    private void UpdateFileList(string file)
    {
        this.directoryFiles.Append("- ");
        this.directoryFiles.AppendLine(file);
        this.directoryFilesText = this.directoryFiles.ToString();
    }

    /// <summary>
    /// Takes the size which the directory scan has added up so far.
    /// </summary>
    /// <remarks>
    /// Two threads call this, but never at the same time: the scan reports its progress from its
    /// own thread, and the final figure follows once that thread has finished. A long is written in
    /// one piece on all six targets we ship, which are 64 bit throughout, and the renderer reads it
    /// only when the refresh timer ticks. The remark on the file list carries the reasoning these
    /// callbacks share.
    /// </remarks>
    private void UpdateDirectorySize(long size)
    {
        this.directorySizeBytes = size;
    }

    /// <summary>
    /// Takes the number of files which the directory scan has counted so far.
    /// </summary>
    /// <remarks>
    /// Reported from the same two threads as the size above, and safe for the same reason.
    /// </remarks>
    private void UpdateDirectoryFiles(long numFiles) => this.directorySizeNumFiles = numFiles;

    /// <summary>
    /// Takes the news that the directory scan has finished.
    /// </summary>
    /// <remarks>
    /// This one, unlike the three above, does not run on the scan's thread. The scan invokes it
    /// after awaiting its worker, and that continuation returns to the dispatcher this dialog was
    /// initialized on. Stopping the timer and asking for a render from here is therefore no
    /// different from doing either in a lifecycle method.
    /// </remarks>
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

    #region Overrides of MSGComponentBase

    protected override async ValueTask DisposeResourcesAsync()
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