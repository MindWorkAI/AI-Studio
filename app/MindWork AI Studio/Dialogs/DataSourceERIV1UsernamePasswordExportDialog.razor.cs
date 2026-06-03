using AIStudio.Components;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceERIV1UsernamePasswordExportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }

    private readonly DataSourceERIUsernamePasswordMode[] availableUsernamePasswordModes =
    [
        DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD,
        DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD
    ];

    private DataSourceERIUsernamePasswordMode usernamePasswordMode = DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD;

    private string GetUsernamePasswordModeText() => this.GetUsernamePasswordModeText(this.usernamePasswordMode);

    private string GetUsernamePasswordModeText(DataSourceERIUsernamePasswordMode mode) => mode switch
    {
        DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD => T("Read each user's username from the operating system and share one password"),
        DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD => T("Use the same username and password for all users"),
        
        _ => T("User-managed username and password"),
    };

    private void Cancel() => this.MudDialog.Cancel();

    private void Export() => this.MudDialog.Close(DialogResult.Ok(new DataSourceERIV1UsernamePasswordExportDialogResult(this.usernamePasswordMode)));
}