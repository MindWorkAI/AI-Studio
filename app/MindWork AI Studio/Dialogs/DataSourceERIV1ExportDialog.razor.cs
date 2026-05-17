using AIStudio.Components;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class DataSourceERIV1ExportDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }

    [Parameter]
    public bool HasConfiguredSecret { get; set; }

    [Parameter]
    public bool CanEncryptSecret { get; set; }

    private readonly DataSourceERIUsernamePasswordMode[] availableUsernamePasswordModes =
    [
        DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD,
        DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD
    ];

    private bool includeSecret;
    private DataSourceERIUsernamePasswordMode usernamePasswordMode = DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD;

    private bool CanIncludeSecret => this.HasConfiguredSecret && this.CanEncryptSecret;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.includeSecret = this.CanIncludeSecret;
        await base.OnInitializedAsync();
    }

    #endregion

    private bool NeedsSecret() => this.DataSource.AuthMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD;

    private string GetIncludeSecretLabel() => this.DataSource.AuthMethod switch
    {
        AuthMethod.TOKEN => T("Include the configured access token in the export?"),
        AuthMethod.USERNAME_PASSWORD => T("Include the configured password in the export?"),
        
        _ => T("Include the configured secret in the export?"),
    };

    private string GetIncludeSecretLabelOn() => this.DataSource.AuthMethod switch
    {
        AuthMethod.TOKEN => T("Yes, export the encrypted access token"),
        AuthMethod.USERNAME_PASSWORD => T("Yes, export the encrypted password"),
        
        _ => T("Yes, export the encrypted secret"),
    };

    private string GetIncludeSecretLabelOff() => this.DataSource.AuthMethod switch
    {
        AuthMethod.TOKEN => T("No, use a token placeholder"),
        AuthMethod.USERNAME_PASSWORD => T("No, use a password placeholder"),
        
        _ => T("No, use a placeholder"),
    };

    private string GetUsernamePasswordModeText() => this.GetUsernamePasswordModeText(this.usernamePasswordMode);

    private string GetUsernamePasswordModeText(DataSourceERIUsernamePasswordMode mode) => mode switch
    {
        DataSourceERIUsernamePasswordMode.OS_USERNAME_SHARED_PASSWORD => T("Read each user's username from the operating system and share one password"),
        DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD => T("Use the same username and password for all users"),
        
        _ => T("User-managed username and password"),
    };

    private void Cancel() => this.MudDialog.Cancel();

    private void Export() => this.MudDialog.Close(DialogResult.Ok(new DataSourceERIV1ExportDialogResult(this.includeSecret, this.usernamePasswordMode)));
}
