using AIStudio.Tools.ChatArchive;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelDataBackup : SettingsPanelBase
{
    /// <summary>
    /// The shortest time between two progress renders. Without this, a large backup or archive
    /// would re-render the whole page once per file or chat.
    /// </summary>
    private static readonly TimeSpan PROGRESS_RENDER_INTERVAL = TimeSpan.FromMilliseconds(100);

    [Inject]
    private ILogger<SettingsPanelDataBackup> Logger { get; init; } = null!;

    [Inject]
    private DataBackupService DataBackupService { get; init; } = null!;

    private ChatArchiveCollisionBehavior collisionBehavior = ChatArchiveCollisionBehavior.SKIP;
    private CancellationTokenSource? cancellationTokenSource;
    private ChatArchiveProgress progress;
    private ChatArchiveImportPreview? importPreview;
    private ChatArchiveImportResult? importResult;
    private DateTimeOffset lastProgressRender;
    private DateTimeOffset lastBackupRender;
    private string selectedArchivePath = string.Empty;

    /// <summary>
    /// Blocks any further action, starting with the click that opens a file dialog. Without
    /// this, a second click during the open dialog would start a second run.
    /// </summary>
    private bool isBusy;

    /// <summary>
    /// Indicates that an import is running, which shows the progress.
    /// </summary>
    private bool isProcessing;

    private bool AllowBackup => this.SettingsManager.ConfigurationData.App.AllowUserToBackUpData;

    private bool AllowChatImport => this.SettingsManager.ConfigurationData.App.AllowUserToRestoreData;

    /// <summary>
    /// Whether the chosen archive can be imported now.
    /// </summary>
    /// <remarks>
    /// The permission is asked here again and not only when choosing the archive: a
    /// configuration plugin may take it away while a chosen archive waits to be imported.
    /// </remarks>
    private bool CanImport => this.importPreview is { Success: true, Manifest.TotalChatCount: > 0 } && this.AllowChatImport;

    private double ProgressPercentage => this.progress.TotalChats is 0 ? 0d : this.progress.ProcessedChats * 100d / this.progress.TotalChats;

    private double BackupProgressPercentage => this.DataBackupService.Progress.Total is 0 ? 0d : this.DataBackupService.Progress.Processed * 100d / this.DataBackupService.Progress.Total;

    private string BackupProgressText => this.DataBackupService.Progress switch
    {
        { Phase: DataBackupPhase.COPYING_INDEXES } current => string.Format(T("Copying the indexes of your data sources: {0} of {1}"), current.Processed + 1, current.Total),
        { Phase: DataBackupPhase.WRITING, Total: > 0 } current => string.Format(T("Writing file {0} of {1}"), current.Processed, current.Total),
        _ => T("Preparing the backup"),
    };

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.DataBackupService.StateChanged += this.OnBackupStateChanged;
        await base.OnInitializedAsync();
    }

    protected override void DisposeResources()
    {
        this.DataBackupService.StateChanged -= this.OnBackupStateChanged;
        this.cancellationTokenSource?.Cancel();
        this.DisposeCancellation();
    }

    #endregion

    /// <summary>
    /// Shows what the backup does, which runs in the service, not here.
    /// </summary>
    private void OnBackupStateChanged()
    {
        // Render at most every PROGRESS_RENDER_INTERVAL, but never skip the end of a backup:
        var now = DateTimeOffset.UtcNow;
        if (this.DataBackupService.IsRunning && now - this.lastBackupRender < PROGRESS_RENDER_INTERVAL)
            return;

        this.lastBackupRender = now;
        _ = this.InvokeAsync(this.StateHasChanged);
    }

    private async Task StartBackupAsync()
    {
        if (this.isBusy || this.DataBackupService.IsRunning || !this.AllowBackup)
            return;

        string archivePath;
        this.isBusy = true;
        try
        {
            var suggestedFileName = $"ai-studio-backup-{DateTime.Now:yyyy-MM-dd}{DataBackupFormat.FILE_EXTENSION}";
            var saveResponse = await this.RustService.SaveFile(T("Back up data"), [FileTypes.BACKUP_ARCHIVE], suggestedFileName);
            if (saveResponse.UserCancelled)
                return;

            archivePath = saveResponse.SaveFilePath;
        }
        finally
        {
            this.isBusy = false;
        }

        //
        // The service runs the backup and reports every step through StateChanged, so leaving the
        // page does not stop it:
        //
        await this.DataBackupService.CreateBackupAsync(archivePath);
    }

    private async Task ChooseArchiveAsync()
    {
        if (this.isBusy || !this.AllowChatImport)
            return;

        this.isBusy = true;
        try
        {
            var selectionResponse = await this.RustService.SelectFile(T("Select a chat archive"), [FileTypes.BACKUP_ARCHIVE]);
            if (selectionResponse.UserCancelled)
                return;

            this.selectedArchivePath = selectionResponse.SelectedFilePath;
            this.importResult = null;
            this.importPreview = await ChatArchiveImporter.ReadPreviewAsync(this.selectedArchivePath, CancellationToken.None);
        }
        finally
        {
            this.isBusy = false;

            // The lockable button calls this as a plain function, not as an event of this
            // component, so nothing renders the preview unless we do:
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private async Task StartImportAsync()
    {
        if (this.isBusy || !this.CanImport)
            return;

        this.isBusy = true;
        try
        {
            this.importResult = null;
            this.isProcessing = true;
            this.progress = new(0, this.importPreview!.Manifest.TotalChatCount);
            this.cancellationTokenSource = new();

            var archivePath = this.selectedArchivePath;
            var token = this.cancellationTokenSource.Token;
            var reporter = this.CreateProgressReporter();
            this.importResult = await Task.Run(() => ChatArchiveImporter.ImportAsync(archivePath, this.collisionBehavior, reporter, token), token);

            //
            // Drop the preview once everything was imported. Otherwise, another click on the
            // import button would silently import the whole archive a second time:
            //
            if (this.importResult is { Success: true, Cancelled: false })
            {
                this.importPreview = null;
                this.selectedArchivePath = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            this.Logger.LogInformation("The user cancelled the chat import.");
        }
        finally
        {
            this.isProcessing = false;
            this.DisposeCancellation();
            this.isBusy = false;
        }
    }

    private void CancelProcessing() => this.cancellationTokenSource?.Cancel();

    /// <summary>
    /// Detaches the cancellation source before disposing it, so that a late click on the
    /// cancel button cannot reach a disposed source.
    /// </summary>
    private void DisposeCancellation()
    {
        var cancellation = this.cancellationTokenSource;
        this.cancellationTokenSource = null;

        cancellation?.Dispose();
    }

    private IProgress<ChatArchiveProgress> CreateProgressReporter()
    {
        this.lastProgressRender = DateTimeOffset.MinValue;
        return new Progress<ChatArchiveProgress>(value =>
        {
            this.progress = value;

            // Render at most every PROGRESS_RENDER_INTERVAL, but never skip the final state:
            var now = DateTimeOffset.UtcNow;
            if (value.ProcessedChats < value.TotalChats && now - this.lastProgressRender < PROGRESS_RENDER_INTERVAL)
                return;

            this.lastProgressRender = now;
            this.InvokeAsync(this.StateHasChanged);
        });
    }
}