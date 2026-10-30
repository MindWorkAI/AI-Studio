using AIStudio.Assistants.ERI;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

using RetrievalInfo = AIStudio.Tools.ERIClient.DataModel.RetrievalInfo;

// ReSharper disable InconsistentNaming
namespace AIStudio.Dialogs;

public partial class DataSourceERI_V1Dialog : MSGComponentBase, ISecretId
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public bool IsEditing { get; set; }
    
    [Parameter]
    public DataSourceERI_V1 DataSource { get; set; }

    /// <summary>
    /// Whether the data source was imported. It then fills the form like an edited one, but the
    /// dialog does not connect to its server before the user tests the connection.
    /// </summary>
    [Parameter]
    public bool IsImport { get; set; }

    /// <summary>
    /// The token or password an imported data source brought along.
    /// </summary>
    [Parameter]
    public string ImportedSecret { get; set; } = string.Empty;

    [Inject]
    private ILogger<DataSourceERI_V1Dialog> Logger { get; init; } = null!;
    
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
    private string importRetrievalIssue = string.Empty;
    private string importedRetrievalId = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    private List<AuthMethod> availableAuthMethods = [];
    private DataSourceSecurity dataSecurityPolicy;
    private OutboundDataRestriction dataOutboundDataRestriction = DataSourceOutboundDataExtensions.DEFAULT_OUTBOUND_DATA_RESTRICTION;
    private SecurityRequirements dataSourceSecurityRequirements;
    private ushort dataMaxMatches = 10;
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
    private List<RetrievalInfo> availableRetrievalProcesses = [];
    private RetrievalInfo dataSelectedRetrievalProcess;
    
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
            GetSecurityRequirements = () => this.dataSourceSecurityRequirements,
        };
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names, those of the mailboxes included:
        this.UsedDataSourcesNames = this.SettingsManager.ConfigurationData.DataSources.Select(x => x.Name.ToLowerInvariant())
            .Concat(this.SettingsManager.ConfigurationData.Mailboxes.Select(x => x.Name.ToLowerInvariant()))
            .ToList();
        
        // An edited data source fills the form, and so does an imported one:
        if(this.IsEditing || this.IsImport)
        {
            //
            // Assign the data to the form fields:
            //
            this.dataNum = this.DataSource.Num;
            this.dataId = this.DataSource.Id;
            this.dataName = this.DataSource.Name;
            this.dataHostname = this.DataSource.Hostname;
            this.dataPort = this.DataSource.Port;
            this.dataAuthMethod = this.DataSource.AuthMethod;
            this.dataUsername = this.DataSource.Username;
            this.dataSecurityPolicy = this.DataSource.SecurityPolicy;
            this.dataOutboundDataRestriction = this.DataSource.OutboundDataRestriction;
            this.dataMaxMatches = this.DataSource.MaxMatches;
            
            // We cannot load the retrieval processes now, since we have
            // to load the data first. But while doing so, we can need to
            // restore the selected retrieval process id. That's why we
            // assign the selected retrieval id to the default retrieval process:
            this.dataSelectedRetrievalProcess = this.dataSelectedRetrievalProcess with { Id = this.DataSource.SelectedRetrievalId };
        }

        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataSource.Name.ToLowerInvariant();
            if (this.dataAuthMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD)
            {
                // Load the secret:
                var requestedSecret = await this.RustService.GetSecret(this, SecretStoreType.DATA_SOURCE);
                if (requestedSecret.Success)
                    this.dataSecret = await requestedSecret.Secret.Decrypt(this.encryption);
                else
                {
                    this.dataSecret = string.Empty;
                    this.dataSecretStorageIssue = string.Format(T("Failed to load the auth. secret from the operating system. The message was: {0}. You might ignore this message and provide the secret again."), requestedSecret.Issue);
                    await this.form.Validate();
                }
            }
            
            // Load the data:
            await this.TestConnection();
            
            // Select the retrieval process:
            this.dataSelectedRetrievalProcess = this.availableRetrievalProcesses.FirstOrDefault(n => n.Id == this.DataSource.SelectedRetrievalId);
        }
        else if (this.IsImport)
        {
            // The first connection test, which the user starts, selects the imported retrieval process:
            this.dataSecret = this.ImportedSecret;
            this.importedRetrievalId = this.DataSource.SelectedRetrievalId;
        }

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            await this.form.ResetValidationAsync();
        
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
            UsernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED,
            Type = DataSourceType.ERI_V1,
            SecurityPolicy = this.dataSecurityPolicy,
            OutboundDataRestriction = this.SelectedOutboundDataRestriction,
            SelectedRetrievalId = this.dataSelectedRetrievalProcess.Id,
            MaxMatches = this.dataMaxMatches,
        };
    }
    
    private OutboundDataRestriction OrganizationMinimumOutboundDataRestriction => this.SettingsManager.ConfigurationData.DataSourceSecurity.MinimumOutboundDataRestriction;

    /// <summary>
    /// The level the data source gets: the one chosen, as far as the organization and the security
    /// policy allow it.
    /// </summary>
    /// <remarks>
    /// Choosing the policy for self-hosted providers rules out every level but the strictest, see
    /// DataSourceOutboundDataExtensions.GetMinimumOutboundDataRestriction. The choice itself stays,
    /// so the dialog shows it again when the user chooses another policy.
    /// </remarks>
    private OutboundDataRestriction SelectedOutboundDataRestriction
    {
        get => this.dataOutboundDataRestriction.StricterOf(DataSourceOutboundDataExtensions.GetMinimumOutboundDataRestriction(this.dataSecurityPolicy, this.OrganizationMinimumOutboundDataRestriction));
        set => this.dataOutboundDataRestriction = value;
    }

    private void ClearImportRetrievalIssue() => this.importRetrievalIssue = string.Empty;

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
            this.DataSource = this.CreateDataSource();
            using var client = ERIClientFactory.Get(ERIVersion.V1, this.DataSource);
            if(client is null)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = T("Failed to connect to the ERI v1 server. The server is not supported.");
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

            var loginResult = await client.AuthenticateAsync(this.RustService, this.dataSecret, cts.Token);
            if (!loginResult.Successful)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = loginResult.Message;
                return;
            }
            
            var securityRequirementsRequest = await client.GetSecurityRequirementsAsync(cts.Token);
            if (!securityRequirementsRequest.Successful)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = securityRequirementsRequest.Message;
                return;
            }
            
            this.dataSourceSecurityRequirements = securityRequirementsRequest.Data;
            
            var retrievalInfoRequest = await client.GetRetrievalInfoAsync(cts.Token);
            if (!retrievalInfoRequest.Successful)
            {
                await this.form.Validate();
                
                Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
                this.dataIssues[^1] = retrievalInfoRequest.Message;
                return;
            }
            
            this.availableRetrievalProcesses = retrievalInfoRequest.Data ?? [];

            // Only the first successful test after an import resolves the imported retrieval ID;
            // afterward, the selection belongs to the user:
            if (!string.IsNullOrWhiteSpace(this.importedRetrievalId))
            {
                var importedRetrieval = this.availableRetrievalProcesses.FirstOrDefault(item => item.Id == this.importedRetrievalId);
                if (importedRetrieval != default)
                    this.dataSelectedRetrievalProcess = importedRetrieval;
                else
                {
                    this.importRetrievalIssue = string.Format(T("The server does not offer the imported retrieval process '{0}'. Please select another one before you save."), this.importedRetrievalId);
                    this.dataSelectedRetrievalProcess = default;
                }

                this.importedRetrievalId = string.Empty;
            }

            this.connectionTested = true;
            this.connectionSuccessfulTested = true;
            this.Logger.LogInformation("Connection to the ERI v1 server was successful tested.");
        }
        catch (Exception e)
        {
            await this.form.Validate();
            
            Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
            this.dataIssues[^1] = string.Format(T("Failed to connect to the ERI v1 server. The message was: {0}"), e.Message);
            this.Logger.LogError($"Failed to connect to the ERI v1 server. Message: {e.Message}");
            
            this.connectionTested = true;
            this.connectionSuccessfulTested = false;
        }
    }

    private string GetTestResultText()
    {
        if(!this.connectionTested)
            return T("Not tested yet.");
        
        return this.connectionSuccessfulTested ? T("Connection successful.") : T("Connection failed.");
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
        AuthMethod.TOKEN => T("Access Token"),
        AuthMethod.USERNAME_PASSWORD => T("Password"),
        _ => T("Secret"),
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
            var storeResponse = await this.RustService.SetSecret(this, this.dataSecret, SecretStoreType.DATA_SOURCE);
            if (!storeResponse.Success)
            {
                this.dataSecretStorageIssue = string.Format(T("Failed to store the auth. secret in the operating system. The message was: {0}. Please try again."), storeResponse.Issue);
                await this.form.Validate();
                return;
            }

            //
            // The OS keyring stores the secret under the name of the data source, so a renamed one
            // got a new entry just now, and the old one goes once the new one is in place. A name
            // which differs only in case is no rename here: some keyrings ignore case, so both
            // names lead to the same entry, and deleting the old one would delete the secret.
            //
            if (this.IsEditing && !string.Equals(this.DataSource.Name, addedDataSource.Name, StringComparison.OrdinalIgnoreCase))
            {
                var deleteResponse = await this.RustService.DeleteSecret(this.DataSource, SecretStoreType.DATA_SOURCE);
                if (!deleteResponse.Success)
                    this.Logger.LogWarning($"Failed to delete the secret of the data source '{this.dataId}' stored under its previous name: {deleteResponse.Issue}");
            }
        }
        
        this.MudDialog.Close(DialogResult.Ok(addedDataSource));
    }
    
    private void Cancel() => this.MudDialog.Cancel();
}