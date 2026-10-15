using System.Globalization;

using AIStudio.Components;
using AIStudio.Tools.DataBackup;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

/// <summary>
/// Tells the user, at the start after a restore, how it went, and what remains to do: entering the API
/// keys and passwords again, for example, since a backup holds none.
/// </summary>
public partial class DataRestoreReportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// How the runtime applied the restore.
    /// </summary>
    [Parameter]
    public DataRestoreReport Report { get; set; } = null!;

    /// <summary>
    /// What remains to do after a restore which was applied.
    /// </summary>
    [Parameter]
    public IReadOnlyList<DataRestoreOpenPoint> OpenPoints { get; set; } = [];

    private CultureInfo currentCultureInfo = CultureInfo.InvariantCulture;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        var activeLanguagePlugin = await this.SettingsManager.GetActiveLanguagePlugin();
        this.currentCultureInfo = CommonTools.DeriveActiveCultureOrInvariant(activeLanguagePlugin.IETFTag);

        await base.OnInitializedAsync();
    }

    #endregion

    private string Icon => this.Report.Outcome switch
    {
        DataRestoreOutcome.APPLIED => Icons.Material.Filled.CheckCircle,
        DataRestoreOutcome.ROLLBACK_FAILED => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.WarningAmber,
    };

    private Color IconColor => this.Report.Outcome switch
    {
        DataRestoreOutcome.APPLIED => Color.Success,
        DataRestoreOutcome.ROLLBACK_FAILED => Color.Error,
        _ => Color.Warning,
    };

    private string Headline => this.Report.Outcome switch
    {
        DataRestoreOutcome.APPLIED => T("Your backup was restored"),
        DataRestoreOutcome.ROLLBACK_FAILED => T("Restoring your backup is not finished"),
        _ => T("Your backup was not restored"),
    };

    private string Explanation => this.Report.Outcome switch
    {
        DataRestoreOutcome.APPLIED => T("AI Studio replaced your data with those of the backup. The plugins and settings of your organization stayed as they were."),
        DataRestoreOutcome.REFUSED => T("AI Studio could not start restoring your backup, so all your data stayed as they were."),
        DataRestoreOutcome.ROLLED_BACK => T("Restoring your backup failed. AI Studio put all your data back the way they were before."),
        DataRestoreOutcome.ROLLBACK_FAILED => T("Restoring your backup failed, and AI Studio could not yet put all your data back the way they were. It tries again at the next start, so please restart AI Studio. If this message appears again, please contact the support, and keep the folders '.restore-previous' and '.restore-pending' in your data folder."),
        _ => string.Empty,
    };

    private string BackupDescription
    {
        get
        {
            if (!DateTimeOffset.TryParse(this.Report.BackupCreatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt))
                return string.Empty;

            return string.Format(T("The backup is from {0} and was made with AI Studio {1}."), CommonTools.FormatTimestampToGeneral(createdAt.ToLocalTime().DateTime, this.currentCultureInfo), this.Report.BackupAppVersion);
        }
    }

    private string Describe(DataRestoreOpenPoint openPoint) => openPoint.Kind switch
    {
        DataRestoreOpenPointKind.PROVIDER_WITHOUT_API_KEY => string.Format(T("Enter the API key of the provider '{0}' again, if it needs one."), openPoint.Name),
        DataRestoreOpenPointKind.EMBEDDING_PROVIDER_WITHOUT_API_KEY => string.Format(T("Enter the API key of the embedding provider '{0}' again, if it needs one."), openPoint.Name),
        DataRestoreOpenPointKind.TRANSCRIPTION_PROVIDER_WITHOUT_API_KEY => string.Format(T("Enter the API key of the transcription provider '{0}' again, if it needs one."), openPoint.Name),
        DataRestoreOpenPointKind.DATA_SOURCE_WITHOUT_SECRET => string.Format(T("Enter the password or token of the data source '{0}' again."), openPoint.Name),
        DataRestoreOpenPointKind.MAILBOX_WITHOUT_PASSWORD => string.Format(T("Enter the password of the mailbox '{0}' again."), openPoint.Name),
        DataRestoreOpenPointKind.DATA_SOURCE_NOT_FOUND => string.Format(T("The data source '{0}' names a file or folder which does not exist on this computer. Choose it anew."), openPoint.Name),
        DataRestoreOpenPointKind.BATCH_PROCESSING_PATH_NOT_FOUND => string.Format(T("The batch processing uses '{0}', which does not exist on this computer."), openPoint.Name),
        DataRestoreOpenPointKind.CERTIFICATE_BUNDLE_NOT_FOUND => string.Format(T("The file with your own root certificates, '{0}', does not exist on this computer."), openPoint.Name),
        DataRestoreOpenPointKind.ERI_SERVER_DIRECTORY_NOT_FOUND => string.Format(T("The ERI server '{0}' writes into a folder which does not exist on this computer."), openPoint.Name),
        _ => openPoint.Name,
    };

    private void Close() => this.MudDialog.Close(DialogResult.Ok(true));
}