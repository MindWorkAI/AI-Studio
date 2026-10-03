using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

using MudBlazor.Interfaces;
using MudBlazor.Utilities;

namespace AIStudio.Dialogs;

/// <summary>
/// Adds or edits a mailbox.
/// </summary>
/// <remarks>
/// Every connection test is exactly one sign-in, started by the user. Its outcome only touches the
/// recorded sign-in failure of the mailbox when the test used the settings and the password stored
/// for it: a failure with a password the user is still typing says nothing about the stored one,
/// and neither does a success.
/// </remarks>
public partial class DataSourceMailboxDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public DataSourceMailbox DataSource { get; set; }

    /// <summary>
    /// Whether the server, the username, and the folder of this mailbox must stay as they are.
    /// </summary>
    /// <remarks>
    /// Set once the index holds something for this mailbox. The embedding is not locked along with
    /// them: it can be changed, and DataSourceReindexWarning asks what that costs.
    /// </remarks>
    [Parameter]
    public bool LockSource { get; set; }

    [Parameter]
    public IReadOnlyList<ConfigurationSelectData<string>> AvailableEmbeddings { get; set; } = [];

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Inject]
    private DatabaseClientProvider DatabaseClientProvider { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<DataSourceMailboxDialog> Logger { get; init; } = null!;

    /// <summary>
    /// How long a connection test may take, signing in and listing the folders included.
    /// </summary>
    private static readonly TimeSpan CONNECTION_TEST_TIMEOUT = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    private readonly DataSourceValidation dataSourceValidation;

    /// <summary>
    /// The names of all data sources and mailboxes. A mailbox needs a name of its own among both.
    /// </summary>
    private List<string> UsedDataSourcesNames { get; set; } = [];

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataSecretStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;

    private uint dataNum;
    private string dataId = Guid.NewGuid().ToString();
    private string dataName = string.Empty;
    private string dataHost = string.Empty;
    private int dataPort = MailboxTransportSecurityExtensions.SSL_ON_CONNECT_PORT;
    private MailboxTransportSecurity dataTransportSecurity = MailboxTransportSecurity.SSL_ON_CONNECT;
    private string dataUsername = string.Empty;
    private string dataPassword = string.Empty;
    private string dataRootFolder = string.Empty;
    private MailboxMaxAge dataMaxAge = MailboxMaxAge.LAST_12_MONTHS;
    private bool dataIndexAttachments = true;
    private int dataMaxAttachmentSizeMegabytes = 10;
    private OutboundDataRestriction dataOutboundDataRestriction = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES;
    private bool dataUserAcknowledgedCloudEmbedding;
    private string dataEmbeddingId = string.Empty;
    private int dataMaxChunkTokenLength;
    private int dataChunkOverlapTokenLength = DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH;
    private ushort dataMaxMatches = 10;
    private ConfidenceLevel dataConfidenceLevel = ConfidenceLevel.NONE;
    private bool showExpertSettings;
    private bool revalidateAfterRender;

    private MailboxProviderTemplate? selectedTemplate;
    private MailboxAuthFailure? authFailure;
    private string storedPassword = string.Empty;
    private ConnectionSettings? testedSettings;
    private MailboxConnectionFailure? testFailure;
    private bool isTestingConnection;
    private IReadOnlyList<MailServerFolder> serverFolders = [];
    private string folderIssue = string.Empty;

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    // The fields whose rules read other fields, see RevalidateDependentFields:
    private MudSelect<string> embeddingSelect = null!;
    private MudSelect<ConfidenceLevel> confidenceLevelSelect = null!;
    private MudNumericField<int> maxChunkTokenLengthField = null!;
    private MudNumericField<int> chunkOverlapTokenLengthField = null!;

    public DataSourceMailboxDialog()
    {
        this.dataSourceValidation = new()
        {
            GetSelectedCloudEmbedding = () => this.SelectedCloudEmbedding,
            GetSelectedEmbeddingProvider = () => this.SelectedEmbedding,
            GetConfidenceLevel = () => this.dataConfidenceLevel,
            GetSettingsManager = () => this.SettingsManager,
            GetPreviousDataSourceName = () => this.dataEditingPreviousInstanceName,
            GetUsedDataSourceNames = () => this.UsedDataSourcesNames,
            GetSecretStorageIssue = () => this.dataSecretStorageIssue,
            GetTestedConnection = () => this.testedSettings == this.CurrentSettings,
            GetTestedConnectionResult = () => this.ConnectionTestSucceeded,
        };
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);

        // A mailbox and a data source must not share a name:
        this.UsedDataSourcesNames = this.SettingsManager.ConfigurationData.DataSources.Select(x => x.Name.ToLowerInvariant())
            .Concat(this.SettingsManager.ConfigurationData.Mailboxes.Select(x => x.Name.ToLowerInvariant()))
            .ToList();

        // When editing, we need to load the data:
        if (this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataSource.Name.ToLowerInvariant();
            this.dataNum = this.DataSource.Num;
            this.dataId = this.DataSource.Id;
            this.dataName = this.DataSource.Name;
            this.dataHost = this.DataSource.Host;
            this.dataPort = this.DataSource.Port;
            this.dataTransportSecurity = this.DataSource.TransportSecurity;
            this.dataUsername = this.DataSource.Username;
            this.dataRootFolder = this.DataSource.RootFolder;
            this.dataMaxAge = this.DataSource.MaxAge;
            this.dataIndexAttachments = this.DataSource.IndexAttachments;
            this.dataMaxAttachmentSizeMegabytes = this.DataSource.MaxAttachmentSizeMegabytes;
            this.dataOutboundDataRestriction = this.DataSource.OutboundDataRestriction;
            this.dataEmbeddingId = this.DataSource.EmbeddingId;
            this.dataMaxChunkTokenLength = this.DataSource.MaxChunkTokenLength;
            this.dataChunkOverlapTokenLength = this.DataSource.ChunkOverlapTokenLength;
            this.dataMaxMatches = this.DataSource.MaxMatches;
            this.dataConfidenceLevel = this.DataSource.ConfidenceLevel;
            this.selectedTemplate = MailboxProviderTemplates.ALL.FirstOrDefault(template => template.Host.Length > 0 && template.Host.Equals(this.DataSource.Host.Trim(), StringComparison.OrdinalIgnoreCase));

            var requestedSecret = await this.RustService.GetSecret(this.DataSource, SecretStoreType.DATA_SOURCE, isTrying: true);
            if (requestedSecret.Success)
            {
                this.storedPassword = await requestedSecret.Secret.Decrypt(Program.ENCRYPTION);
                this.dataPassword = this.storedPassword;
            }
            else
                this.dataSecretStorageIssue = string.Format(T("Failed to load the password from the operating system. The message was: {0}. You might ignore this message and provide the password again."), requestedSecret.Issue);

            var indexStore = await this.DatabaseClientProvider.GetIndexStoreAsync();
            this.authFailure = await indexStore.GetMailboxAuthFailureAsync(this.dataId, CancellationToken.None);
        }

        // A level the organization ruled out after the mailbox was saved is not offered anymore, so
        // the dialog starts with the one which applies anyway, see MailToolResults.GetRequirements:
        this.dataOutboundDataRestriction = this.dataOutboundDataRestriction.StricterOf(this.MinimumOutboundDataRestriction);

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if (!this.IsEditing && firstRender)
            this.form.ResetValidation();

        // A check asked for in code waits until the fields hold their new values, cf. ToggleExpertSettings:
        if (this.revalidateAfterRender)
        {
            this.revalidateAfterRender = false;
            await this.RevalidateDependentFields(changedField: null);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    /// <summary>
    /// What a connection test depends on. A test only counts for the settings it was made with.
    /// </summary>
    private readonly record struct ConnectionSettings(string Host, int Port, MailboxTransportSecurity TransportSecurity, string Username, string Password);

    private ConnectionSettings CurrentSettings => new(this.dataHost.Trim(), this.dataPort, this.dataTransportSecurity, this.dataUsername.Trim(), this.dataPassword);

    private ConnectionSettings StoredSettings => new(this.DataSource.Host.Trim(), this.DataSource.Port, this.DataSource.TransportSecurity, this.DataSource.Username.Trim(), this.storedPassword);

    /// <summary>
    /// Whether the settings to connect with are the ones stored for this mailbox.
    /// </summary>
    private bool UsesStoredSettings(ConnectionSettings settings) => this.IsEditing && settings == this.StoredSettings;

    /// <summary>
    /// A new mailbox is only added once a sign-in worked, and an edited one once more when its connection changed.
    /// </summary>
    private bool RequiresConnectionTest => !this.UsesStoredSettings(this.CurrentSettings);

    private bool ConnectionTestSucceeded => this.testedSettings == this.CurrentSettings && this.testFailure is null;

    private bool CanChangeSource => !this.IsEditing || !this.LockSource;

    private bool CanTestConnection => !this.isTestingConnection
        && DataSourceValidation.ValidateMailboxHost(this.dataHost) is null
        && this.dataSourceValidation.ValidatePort(this.dataPort) is null
        && DataSourceValidation.ValidateMailboxTransportSecurity(this.dataTransportSecurity) is null
        && DataSourceValidation.ValidateMailboxUsername(this.dataUsername) is null
        && !string.IsNullOrEmpty(this.dataPassword);

    /// <summary>
    /// The name of the chosen provider template, or empty for another provider.
    /// </summary>
    private string SelectedTemplateName => this.selectedTemplate?.Name ?? string.Empty;

    private string SelectedTemplateText => this.selectedTemplate?.Name ?? T("Another provider");

    private string SignInGroupClass => this.authFailure is null
        ? "border-dashed border rounded-lg pa-3 mb-6"
        : "border-dashed border-2 mud-border-error rounded-lg pa-3 mb-6";

    private string AuthFailureText => this.authFailure is null
        ? string.Empty
        : string.Format(T("Signing in to this mailbox failed on {0}. Presumably your password changed. AI Studio does not try again on its own, so that your account is not locked. Enter your new password below and save it, or test the connection."), this.authFailure.FailedAtUtc.ToLocalTime().ToString("g", I18N.I.Culture));

    private string RootFolderText => string.IsNullOrEmpty(this.dataRootFolder) ? T("Whole mailbox") : this.dataRootFolder;

    private string ConfidenceLevelText => this.dataConfidenceLevel is ConfidenceLevel.NONE ? string.Empty : this.dataConfidenceLevel.GetName();

    private OutboundDataRestriction MinimumOutboundDataRestriction => this.SettingsManager.ConfigurationData.MailboxSettings.MinimumOutboundDataRestriction;

    private bool IsOutboundDataRestrictionLimited => this.MinimumOutboundDataRestriction is not OutboundDataRestriction.UNRESTRICTED;

    private string TestResultText
    {
        get
        {
            if (this.isTestingConnection)
                return T("Testing the connection ...");

            if (this.testedSettings != this.CurrentSettings)
                return this.RequiresConnectionTest ? T("Not tested yet.") : string.Empty;

            return this.testFailure is { } failure ? failure.GetDescription() : T("Connection successful.");
        }
    }

    private Color TestResultColor => this.testedSettings != this.CurrentSettings
        ? Color.Default
        : this.testFailure is null ? Color.Success : Color.Error;

    private string TestResultIcon => this.testedSettings != this.CurrentSettings
        ? Icons.Material.Outlined.HourglassEmpty
        : this.testFailure is null ? Icons.Material.Outlined.CheckCircle : Icons.Material.Outlined.Error;

    private EmbeddingProvider? GetEmbeddingProvider(string providerId)
    {
        var provider = this.SettingsManager.GetEmbeddingProviderById(providerId);
        return provider == EmbeddingProvider.NONE ? null : provider;
    }

    private EmbeddingProvider? SelectedEmbedding => this.SettingsManager.ConfigurationData.EmbeddingProviders
        .FirstOrDefault(x => x.Id == this.dataEmbeddingId);

    private bool SelectedCloudEmbedding => this.SelectedEmbedding is { IsSelfHosted: false };

    private int ProviderMaxChunkTokenLength => this.SelectedEmbedding?.EffectiveTokenLimit ?? EmbeddingProvider.DEFAULT_TOKEN_LIMIT;

    private string MaxChunkTokenLengthHelperText => string.Format(
        T("Maximum number of tokens per chunk for this data source. The embedding provider default is {0} tokens."),
        this.ProviderMaxChunkTokenLength);

    private string ChunkOverlapTokenLengthHelperText => string.Format(
        T("Number of tokens repeated at the start of the next chunk. The default overlap is {0} tokens."),
        DataSourceEmbeddingService.DEFAULT_CHUNK_OVERLAP_TOKEN_LENGTH);

    private DataSourceMailbox CreateDataSource() => new()
    {
        Id = this.dataId,
        Num = this.dataNum,
        Name = this.dataName,
        Type = DataSourceType.MAILBOX,
        EmbeddingId = this.dataEmbeddingId,
        MaxChunkTokenLength = this.dataMaxChunkTokenLength,
        ChunkOverlapTokenLength = this.dataChunkOverlapTokenLength,
        ConfidenceLevel = this.dataConfidenceLevel,

        // Kept out of reach of the form while the source is locked, so a stale field cannot point an
        // indexed mailbox somewhere else:
        Host = this.CanChangeSource ? this.dataHost.Trim() : this.DataSource.Host,
        Username = this.CanChangeSource ? this.dataUsername.Trim() : this.DataSource.Username,
        RootFolder = this.CanChangeSource ? this.dataRootFolder : this.DataSource.RootFolder,

        Port = this.dataPort,
        TransportSecurity = this.dataTransportSecurity,
        AuthMethod = MailboxAuthMethod.PASSWORD,
        MaxAge = this.dataMaxAge,
        IndexAttachments = this.dataIndexAttachments,
        MaxAttachmentSizeMegabytes = this.dataMaxAttachmentSizeMegabytes,
        OutboundDataRestriction = this.dataOutboundDataRestriction,
        MaxMatches = this.dataMaxMatches,
    };

    private void SelectTemplate(string templateName)
    {
        var template = MailboxProviderTemplates.ALL.FirstOrDefault(candidate => candidate.Name == templateName);
        this.selectedTemplate = template;
        if (template is null)
            return;

        this.dataHost = template.Host;
        this.dataPort = template.Port;
        this.dataTransportSecurity = template.TransportSecurity;
    }

    private void SelectTransportSecurity(MailboxTransportSecurity transportSecurity)
    {
        //
        // Who switches the encryption usually means the port that comes with it, unless they
        // entered a port of their own:
        //
        if (this.dataPort == this.dataTransportSecurity.GetUsualPort() && transportSecurity.GetUsualPort() is { } usualPort)
            this.dataPort = usualPort;

        this.dataTransportSecurity = transportSecurity;
    }

    private async Task TestConnection()
    {
        if (!this.CanTestConnection)
            return;

        var settings = this.CurrentSettings;
        var serverAnswer = string.Empty;
        this.isTestingConnection = true;
        this.folderIssue = string.Empty;

        try
        {
            using var timeout = new CancellationTokenSource(CONNECTION_TEST_TIMEOUT);
            await using var connector = new ImapMailboxConnector();
            await connector.ConnectAsync(this.CreateDataSource(), settings.Password, timeout.Token);
            this.serverFolders = await connector.GetFoldersAsync(timeout.Token);
            this.testFailure = null;
            this.Logger.LogInformation($"Tested the connection of the mailbox '{this.dataId}' successfully.");
        }
        catch (MailboxConnectionException e)
        {
            this.serverFolders = [];
            this.testFailure = e.Failure;
            serverAnswer = e.InnerException?.Message ?? string.Empty;
            this.Logger.LogWarning($"Testing the connection of the mailbox '{this.dataId}' failed: {e.Failure} ({e.InnerException?.GetType().Name ?? "no inner exception"}).");
        }
        catch (OperationCanceledException)
        {
            this.serverFolders = [];
            this.testFailure = MailboxConnectionFailure.NETWORK_UNAVAILABLE;
            this.Logger.LogWarning($"Testing the connection of the mailbox '{this.dataId}' took too long.");
        }
        finally
        {
            this.testedSettings = settings;
            this.isTestingConnection = false;
        }

        await this.RecordSignInOutcomeAsync(settings, this.testFailure, serverAnswer);
        await this.form.Validate();
    }

    private async Task<MailServerFolder?> CreateFolder(string parentFullName, string name)
    {
        var settings = this.CurrentSettings;
        var serverAnswer = string.Empty;
        this.folderIssue = string.Empty;

        MailboxConnectionFailure? failure = null;
        try
        {
            using var timeout = new CancellationTokenSource(CONNECTION_TEST_TIMEOUT);
            await using var connector = new ImapMailboxConnector();
            await connector.ConnectAsync(this.CreateDataSource(), settings.Password, timeout.Token);
            var createdFolder = await connector.CreateFolderAsync(parentFullName, name, timeout.Token);

            this.serverFolders = [..this.serverFolders, createdFolder];
            this.Logger.LogInformation($"Created a folder in the mailbox '{this.dataId}'.");
            return createdFolder;
        }
        catch (MailboxConnectionException e)
        {
            failure = e.Failure;
            serverAnswer = e.InnerException?.Message ?? string.Empty;
            this.folderIssue = e.Failure is MailboxConnectionFailure.SERVER_ERROR
                ? T("The server did not create the folder. Perhaps a folder of this name exists already.")
                : e.Failure.GetDescription();

            this.Logger.LogWarning($"Creating a folder in the mailbox '{this.dataId}' failed: {e.Failure} ({e.InnerException?.GetType().Name ?? "no inner exception"}).");
            return null;
        }
        catch (ArgumentException)
        {
            this.folderIssue = T("This name is too long, or it contains a character the server reserves for folder paths.");
            return null;
        }
        catch (OperationCanceledException)
        {
            this.folderIssue = MailboxConnectionFailure.NETWORK_UNAVAILABLE.GetDescription();
            return null;
        }
        finally
        {
            if (failure is MailboxConnectionFailure.AUTHENTICATION_FAILED)
                await this.RecordSignInOutcomeAsync(settings, failure, serverAnswer);
        }
    }

    /// <summary>
    /// Records what a sign-in with the stored settings showed, cf. the remarks of this dialog.
    /// </summary>
    /// <param name="settings">The settings the sign-in used.</param>
    /// <param name="failure">Why the sign-in failed, or null when it worked.</param>
    /// <param name="serverAnswer">What the server answered to a failed sign-in, which often says why, e.g., that it requires an app password.</param>
    private async Task RecordSignInOutcomeAsync(ConnectionSettings settings, MailboxConnectionFailure? failure, string serverAnswer)
    {
        if (!this.UsesStoredSettings(settings))
            return;

        var indexStore = await this.DatabaseClientProvider.GetIndexStoreAsync();
        switch (failure)
        {
            case null:
                await indexStore.ClearMailboxAuthFailureAsync(this.dataId, CancellationToken.None);
                this.authFailure = null;
                break;

            case MailboxConnectionFailure.AUTHENTICATION_FAILED:
                this.authFailure = MailboxAuthFailure.FromServerAnswer(serverAnswer);
                await indexStore.UpsertMailboxAuthFailureAsync(this.dataId, this.authFailure, CancellationToken.None);
                break;
        }
    }

    private async Task Store()
    {
        await this.form.Validate();

        if (this.RequiresConnectionTest && this.dataSourceValidation.ValidateTestedConnection() is { } testIssue)
        {
            Array.Resize(ref this.dataIssues, this.dataIssues.Length + 1);
            this.dataIssues[^1] = testIssue;
            this.dataIsValid = false;
        }

        this.dataSecretStorageIssue = string.Empty;

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        var mailbox = this.CreateDataSource();

        //
        // Only when editing: while adding, DataSource is still default, and nothing has been
        // prepared for a mailbox which does not exist yet.
        //
        if (this.IsEditing && !await DataSourceReindexWarning.ConfirmDataSourceChangeAsync(this.DialogService, this.SettingsManager, this.DataSourceEmbeddingService, this.DataSource, mailbox))
            return;

        //
        // The OS keyring stores the password under the name of the mailbox, so a renamed mailbox
        // gets a new entry, and the old one goes once the new one is in place:
        //
        var isRenamed = this.IsEditing && !string.Equals(this.DataSource.Name, mailbox.Name, StringComparison.Ordinal);
        var isNewPassword = !this.IsEditing || !string.Equals(this.dataPassword, this.storedPassword, StringComparison.Ordinal);
        if (isNewPassword || isRenamed)
        {
            var storeResponse = await this.RustService.SetSecret(mailbox, this.dataPassword, SecretStoreType.DATA_SOURCE);
            if (!storeResponse.Success)
            {
                this.dataSecretStorageIssue = string.Format(T("Failed to store the password in the operating system. The message was: {0}. Please try again."), storeResponse.Issue);
                await this.form.Validate();
                return;
            }
        }

        if (isRenamed)
        {
            var deleteResponse = await this.RustService.DeleteSecret(this.DataSource, SecretStoreType.DATA_SOURCE);
            if (!deleteResponse.Success)
                this.Logger.LogWarning($"Failed to delete the password of the mailbox '{this.dataId}' stored under its previous name: {deleteResponse.Issue}");
        }

        // A new password earns the next synchronization one attempt to sign in:
        if (this.IsEditing && isNewPassword)
        {
            var indexStore = await this.DatabaseClientProvider.GetIndexStoreAsync();
            await indexStore.ClearMailboxAuthFailureAsync(this.dataId, CancellationToken.None);
        }

        this.MudDialog.Close(DialogResult.Ok(mailbox));
    }

    private void Cancel() => this.MudDialog.Cancel();

    /// <summary>
    /// Gives the fields which are checked against each other a fresh verdict: the embedding provider
    /// and the required confidence level, and the token limits, which depend on the embedding provider.
    /// </summary>
    private Task RevalidateDependentFields(IFormComponent? changedField) => DependentFieldValidation.RevalidateAsync(changedField, this.embeddingSelect, this.confidenceLevelSelect, this.maxChunkTokenLengthField, this.chunkOverlapTokenLengthField);

    private Task RevalidateAfterFieldChange(FormFieldChangedEventArgs change) => this.RevalidateDependentFields(change.Field);

    private string? ValidateMaxChunkTokenLength(int maxChunkTokenLength)
    {
        if (!this.showExpertSettings)
            return null;

        if (maxChunkTokenLength < 1)
            return T("Please enter a token limit of at least 1.");

        var providerMaxChunkTokenLength = this.ProviderMaxChunkTokenLength;
        if (maxChunkTokenLength > providerMaxChunkTokenLength)
            return string.Format(T("The data source token limit must not be larger than the embedding provider token limit ({0})."), providerMaxChunkTokenLength);

        return null;
    }

    private string? ValidateChunkOverlapTokenLength(int chunkOverlapTokenLength)
    {
        if (!this.showExpertSettings)
            return null;

        if (chunkOverlapTokenLength < 0)
            return T("Please enter 0 or a positive overlap length.");

        var effectiveMaxChunkTokenLength = this.dataMaxChunkTokenLength > 0
            ? this.dataMaxChunkTokenLength
            : this.ProviderMaxChunkTokenLength;
        if (chunkOverlapTokenLength >= effectiveMaxChunkTokenLength)
            return T("The overlap must be smaller than the effective token limit.");

        return null;
    }

    private void ToggleExpertSettings()
    {
        this.showExpertSettings = !this.showExpertSettings;
        if (this.showExpertSettings && this.dataMaxChunkTokenLength < 1)
            this.dataMaxChunkTokenLength = this.ProviderMaxChunkTokenLength;

        // The token limits are only checked while they are shown. The field learns the limit set
        // above only with the next render, so it is checked after that:
        this.revalidateAfterRender = true;
    }

    private string GetExpertStyles => this.showExpertSettings ? "border-2 border-dashed rounded pa-2" : string.Empty;
}