using AIStudio.Assistants.ERI;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

// ReSharper disable InconsistentNaming
namespace AIStudio.Dialogs;

public partial class DataSourceERI_V1Dialog : ComponentBase, ISecretId
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private ILogger<ProviderDialog> Logger { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    private readonly DataSourceValidation dataSourceValidation;
    private readonly Encryption encryption = Program.ENCRYPTION;
    
    /// <summary>
    /// The list of used data source names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedDataSourcesNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataSecretStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    private List<AuthMethod> availableAuthMethods = [];
    private bool connectionTested;
    private bool connectionSuccessfulTested;
    
    private uint dataNum;
    private string dataSecret = string.Empty;
    private string dataId = Guid.NewGuid().ToString();
    private string dataName = string.Empty;
    private string dataHostname = string.Empty;
    private int dataPort;
    private AuthMethod dataAuthMethod;
    private string dataUsername = string.Empty;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    public DataSourceERI_V1Dialog()
    {
        this.dataSourceValidation = new()
        {
            GetAuthMethod = () => this.dataAuthMethod,
            GetPreviousDataSourceName = () => this.dataEditingPreviousInstanceName,
            GetUsedDataSourceNames = () => this.UsedDataSourcesNames,
            GetSecretStorageIssue = () => this.dataSecretStorageIssue,
            GetTestedConnection = () => this.connectionTested,
            GetTestedConnectionResult = () => this.connectionSuccessfulTested,
            GetAvailableAuthMethods = () => this.availableAuthMethods,
        };
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedDataSourcesNames = this.SettingsManager.ConfigurationData.DataSources.Select(x => x.Name.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataSource.Name.ToLowerInvariant();
            this.dataNum = this.DataSource.Num;
            this.dataId = this.DataSource.Id;
            this.dataName = this.DataSource.Name;
            this.dataHostname = this.DataSource.Hostname;
            this.dataPort = this.DataSource.Port;
            this.dataAuthMethod = this.DataSource.AuthMethod;
            this.dataUsername = this.DataSource.Username;

            if (this.dataAuthMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD)
            {
                // Load the secret:
                var requestedSecret = await this.RustService.GetSecret(this);
                if (requestedSecret.Success)
                    this.dataSecret = await requestedSecret.Secret.Decrypt(this.encryption);
                else
                {
                    this.dataSecret = string.Empty;
                    this.dataSecretStorageIssue = $"Failed to load the auth. secret from the operating system. The message was: {requestedSecret.Issue}. You might ignore this message and provide the secret again.";
                    await this.form.Validate();
                }
            }
        }
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    #region Implementation of ISecretId

    public string SecretId => this.dataId;
    
    public string SecretName => this.dataName;

    #endregion
    
    private DataSourceERI_V1 CreateDataSource()
    {
        var cleanedHostname = this.dataHostname.Trim();
        return new DataSourceERI_V1
        {
            Id = this.dataId,
            Num = this.dataNum,
            Port = this.dataPort,
            Name = this.dataName,
            Hostname = cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname,
            AuthMethod = this.dataAuthMethod,
            Username = this.dataUsername,
            Type = DataSourceType.ERI_V1,
        };
    }
    
    private bool IsConnectionEncrypted() => this.dataHostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase);

    private bool IsConnectionPossible()
    {
        if(this.dataSourceValidation.ValidatingHostname(this.dataHostname) is not null)
            return false;
        
        if(this.dataSourceValidation.ValidatePort(this.dataPort) is not null)
            return false;
        
        return true;
    }

    private async Task TestConnection()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(14));
            var dataSource = new DataSourceERI_V1
            {
                Hostname = this.dataHostname,
                Port = this.dataPort
            };

            using var client = ERIClientFactory.Get(ERIVersion.V1, dataSource);
            if(client is null)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = "Failed to connect to the ERI v1 server. The server is not supported.";
                return;
            }
            
            var authSchemes = await client.GetAuthMethodsAsync(cts.Token);
            if (!authSchemes.Successful)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = authSchemes.Message;
                return;
            }

            this.availableAuthMethods = authSchemes.Data!.Select(n => n.AuthMethod).ToList();

            this.connectionTested = true;
            this.connectionSuccessfulTested = true;
            this.Logger.LogInformation("Connection to the ERI v1 server was successful tested.");
        }
        catch (Exception e)
        {
            await this.form.Validate();
            
            Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
            this.dataIssues[^1] = $"Failed to connect to the ERI v1 server. The message was: {e.Message}";
            this.Logger.LogError($"Failed to connect to the ERI v1 server. Message: {e.Message}");
            
            this.connectionTested = true;
            this.connectionSuccessfulTested = false;
        }
    }

    private string GetTestResultText()
    {
        if(!this.connectionTested)
            return "Not tested yet.";
        
        return this.connectionSuccessfulTested ? "Connection successful." : "Connection failed.";
    }
    
    private Color GetTestResultColor()
    {
        if (!this.connectionTested)
            return Color.Default;
        
        return this.connectionSuccessfulTested ? Color.Success : Color.Error;
    }
    
    private string GetTestResultIcon()
    {
        if (!this.connectionTested)
            return Icons.Material.Outlined.HourglassEmpty;
        
        return this.connectionSuccessfulTested ? Icons.Material.Outlined.CheckCircle : Icons.Material.Outlined.Error;
    }
    
    private bool NeedsSecret() => this.dataAuthMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD;

    private string GetSecretLabel() => this.dataAuthMethod switch
    {
        AuthMethod.TOKEN => "Access Token",
        AuthMethod.USERNAME_PASSWORD => "Password",
        _ => "Secret",
    };

    private async Task Store()
    {
        await this.form.Validate();
        
        var testConnectionValidation = this.dataSourceValidation.ValidateTestedConnection();
        if(testConnectionValidation is not null)
        {
            Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
            this.dataIssues[^1] = testConnectionValidation;
            this.dataIsValid = false;
        }
        
        this.dataSecretStorageIssue = string.Empty;
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        var addedDataSource = this.CreateDataSource();
        if (!string.IsNullOrWhiteSpace(this.dataSecret))
        {
            // Store the secret in the OS secure storage:
            var storeResponse = await this.RustService.SetSecret(this, this.dataSecret);
            if (!storeResponse.Success)
            {
                this.dataSecretStorageIssue = $"Failed to store the auth. secret in the operating system. The message was: {storeResponse.Issue}. Please try again.";
                await this.form.Validate();
                return;
            }
        }
        
        this.MudDialog.Close(DialogResult.Ok(addedDataSource));
    }
    
    private void Cancel() => this.MudDialog.Cancel();
}