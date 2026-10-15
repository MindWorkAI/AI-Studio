using System.Globalization;

using AIStudio.Dialogs;
using AIStudio.Tools.ChatArchive;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Backs up all data into one file, and restores such a backup: as a whole, or only its chats.
/// </summary>
/// <remarks>
/// It appears on the information page and in the settings. A backup and the preparation of a restore
/// run in their services, so leaving the page does not stop them; this component only shows them.
/// Adding chats runs here, and leaving the page stops it.
/// </remarks>
public partial class DataBackupPanel : MSGComponentBase
{
    /// <summary>
    /// The shortest time between two progress renders. Without this, a large backup would re-render
    /// the whole page once per file or chat.
    /// </summary>
    private static readonly TimeSpan PROGRESS_RENDER_INTERVAL = TimeSpan.FromMilliseconds(100);

    [Inject]
    private ILogger<DataBackupPanel> Logger { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private DataBackupService DataBackupService { get; init; } = null!;

    [Inject]
    private DataRestoreService DataRestoreService { get; init; } = null!;

    private ChatArchiveCollisionBehavior collisionBehavior = ChatArchiveCollisionBehavior.SKIP;
    private CancellationTokenSource? importCancellation;
    private ChatArchiveProgress importProgress;
    private ChatArchiveImportPreview? preview;
    private ChatArchiveImportResult? importResult;
    private DateTimeOffset lastImportRender;
    private DateTimeOffset lastServiceRender;
    private string selectedArchivePath = string.Empty;

    /// <summary>
    /// The culture of the active language plugin, which dates are shown in.
    /// </summary>
    private CultureInfo currentCultureInfo = CultureInfo.InvariantCulture;

    /// <summary>
    /// Blocks any further action, starting with the click that opens a file dialog. Without this, a
    /// second click during the open dialog would start a second run.
    /// </summary>
    private bool isBusy;

    /// <summary>
    /// Whether chats are being added, which shows their progress.
    /// </summary>
    private bool isImporting;

    private bool AllowBackup => this.SettingsManager.ConfigurationData.App.AllowUserToBackUpData;

    private bool AllowRestore => this.SettingsManager.ConfigurationData.App.AllowUserToRestoreData;

    /// <summary>
    /// Whether the chosen backup can replace all data now.
    /// </summary>
    /// <remarks>
    /// The permission is asked here again and not only when choosing the backup: a configuration
    /// plugin may take it away while a chosen backup waits.
    /// </remarks>
    private bool CanRestore => this.preview is { Success: true } && this.AllowRestore && !this.DataRestoreService.IsRestoreUnfinished;

    /// <summary>
    /// Whether the chats of the chosen backup can be added now.
    /// </summary>
    private bool CanImport => this.preview is { Success: true, Manifest.TotalChatCount: > 0 } && this.AllowRestore && !this.DataRestoreService.IsPreparing;

    private double ImportProgressPercentage => this.importProgress.TotalChats is 0 ? 0d : this.importProgress.ProcessedChats * 100d / this.importProgress.TotalChats;

    private double BackupProgressPercentage => this.DataBackupService.Progress.Total is 0 ? 0d : this.DataBackupService.Progress.Processed * 100d / this.DataBackupService.Progress.Total;

    private double RestoreProgressPercentage => this.DataRestoreService.Progress.Total is 0 ? 0d : this.DataRestoreService.Progress.Processed * 100d / this.DataRestoreService.Progress.Total;

    private string BackupProgressText => this.DataBackupService.Progress switch
    {
        { Phase: DataBackupPhase.COPYING_INDEXES } current => string.Format(T("Copying the indexes of your data sources: {0} of {1}"), current.Processed + 1, current.Total),
        { Phase: DataBackupPhase.WRITING, Total: > 0 } current => string.Format(T("Writing file {0} of {1}"), current.Processed, current.Total),
        _ => T("Preparing the backup"),
    };

    private string RestoreProgressText => this.DataRestoreService.Progress is { Total: > 0 } current
        ? string.Format(T("Unpacking file {0} of {1}"), current.Processed, current.Total)
        : T("Reading the backup");

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.DataBackupService.StateChanged += this.OnServiceStateChanged;
        this.DataRestoreService.StateChanged += this.OnServiceStateChanged;

        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCultureInfo = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);

        await base.OnInitializedAsync();
    }

    protected override void DisposeResources()
    {
        this.DataBackupService.StateChanged -= this.OnServiceStateChanged;
        this.DataRestoreService.StateChanged -= this.OnServiceStateChanged;
        this.importCancellation?.Cancel();
        this.DisposeImportCancellation();
    }

    #endregion

    /// <summary>
    /// The number of chats, in the singular for one of them.
    /// </summary>
    private string ChatCount(int count) => count is 1 ? T("1 chat") : string.Format(T("{0} chats"), count);

    private string AddChatsText(int count) => count is 1 ? T("Add 1 chat") : string.Format(T("Add {0} chats"), count);

    /// <summary>
    /// Shows what a backup or the preparation of a restore does, which runs in its service, not here.
    /// </summary>
    private void OnServiceStateChanged()
    {
        // Render at most every PROGRESS_RENDER_INTERVAL, but never skip the end of a run:
        var now = DateTimeOffset.UtcNow;
        var isRunning = this.DataBackupService.IsRunning || this.DataRestoreService.IsPreparing;
        if (isRunning && now - this.lastServiceRender < PROGRESS_RENDER_INTERVAL)
            return;

        this.lastServiceRender = now;
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
        if (this.isBusy || !this.AllowRestore)
            return;

        this.isBusy = true;
        try
        {
            var selectionResponse = await this.RustService.SelectFile(T("Select a backup"), [FileTypes.BACKUP_ARCHIVE]);
            if (selectionResponse.UserCancelled)
                return;

            this.selectedArchivePath = selectionResponse.SelectedFilePath;
            this.importResult = null;
            this.preview = await ChatArchiveImporter.ReadPreviewAsync(this.selectedArchivePath, CancellationToken.None);
        }
        finally
        {
            this.isBusy = false;

            // The lockable button calls this as a plain function, not as an event of this
            // component, so nothing renders the preview unless we do:
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    /// <summary>
    /// Asks whether to replace all data with those of the chosen backup, and prepares the restore.
    /// </summary>
    /// <remarks>
    /// Once the restore is prepared, the main layout asks for the restart and blocks everything else
    /// until then: whatever changes meanwhile would be lost.
    /// </remarks>
    private async Task StartRestoreAsync()
    {
        if (this.isBusy || !this.CanRestore)
            return;

        this.isBusy = true;
        try
        {
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("Restoring this backup replaces your settings, your chats, your plugins, and the indexes of your data sources with those of the backup. Whatever you changed since the backup was made is lost. Afterward, AI Studio restarts. Do you want to restore this backup?") },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Replace All Data"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is null || dialogResult.Canceled)
                return;
        }
        finally
        {
            this.isBusy = false;
        }

        await this.DataRestoreService.PrepareRestoreAsync(this.selectedArchivePath);
    }

    private async Task StartImportAsync()
    {
        if (this.isBusy || !this.CanImport)
            return;

        this.isBusy = true;
        try
        {
            this.importResult = null;
            this.isImporting = true;
            this.importProgress = new(0, this.preview!.Manifest.TotalChatCount);
            this.importCancellation = new();

            var archivePath = this.selectedArchivePath;
            var token = this.importCancellation.Token;
            var reporter = this.CreateImportProgressReporter();
            this.importResult = await Task.Run(() => ChatArchiveImporter.ImportAsync(archivePath, this.collisionBehavior, reporter, token), token);

            //
            // Drop the preview once every chat was added. Otherwise, another click on the button
            // would silently add all of them a second time:
            //
            if (this.importResult is { Success: true, Cancelled: false })
            {
                this.preview = null;
                this.selectedArchivePath = string.Empty;
            }
        }
        catch (OperationCanceledException)
        {
            this.Logger.LogInformation("The user stopped adding the chats of a backup.");
        }
        finally
        {
            this.isImporting = false;
            this.DisposeImportCancellation();
            this.isBusy = false;
        }
    }

    private void CancelImport() => this.importCancellation?.Cancel();

    /// <summary>
    /// Detaches the cancellation source before disposing it, so that a late click on the cancel
    /// button cannot reach a disposed source.
    /// </summary>
    private void DisposeImportCancellation()
    {
        var cancellation = this.importCancellation;
        this.importCancellation = null;

        cancellation?.Dispose();
    }

    private IProgress<ChatArchiveProgress> CreateImportProgressReporter()
    {
        this.lastImportRender = DateTimeOffset.MinValue;
        return new Progress<ChatArchiveProgress>(value =>
        {
            this.importProgress = value;

            // Render at most every PROGRESS_RENDER_INTERVAL, but never skip the final state:
            var now = DateTimeOffset.UtcNow;
            if (value.ProcessedChats < value.TotalChats && now - this.lastImportRender < PROGRESS_RENDER_INTERVAL)
                return;

            this.lastImportRender = now;
            this.InvokeAsync(this.StateHasChanged);
        });
    }
}