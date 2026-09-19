using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Settings;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Dialogs;

public partial class EmbeddingProviderDialog : MSGComponentBase, ISecretId
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The embedding's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }
    
    /// <summary>
    /// The embedding's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The user chosen name.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;
    
    /// <summary>
    /// The chosen hostname for self-hosted providers.
    /// </summary>
    [Parameter]
    public string DataHostname { get; set; } = string.Empty;
    
    /// <summary>
    /// The host to use, e.g., llama.cpp.
    /// </summary>
    [Parameter]
    public Host DataHost { get; set; } = Host.NONE;
    
    /// <summary>
    /// Is this provider self-hosted?
    /// </summary>
    [Parameter]
    public bool IsSelfHosted { get; set; }
    
    /// <summary>
    /// The provider to use.
    /// </summary>
    [Parameter]
    public LLMProviders DataLLMProvider { get; set; } = LLMProviders.NONE;

    /// <summary>
    /// The validated custom icon supplied by a configuration plugin.
    /// </summary>
    [Parameter]
    public string DataCustomIconDataUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// The embedding model to use.
    /// </summary>
    [Parameter]
    public Model DataModel { get; set; }

    /// <summary>
    /// The Hugging Face inference provider to use.
    /// </summary>
    [Parameter]
    public HFInferenceProvider HFInferenceProviderId { get; set; } = HFInferenceProvider.NONE;
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }

    [Parameter]
    public string DataTokenizerPath { get; set; } = string.Empty;

    /// <summary>
    /// The fingerprint of the tokenizer this provider was stored with.
    /// </summary>
    /// <remarks>
    /// Carried through the dialog untouched as long as the user leaves the tokenizer alone. Rebuilding
    /// it from the path on every open would read a file for nothing, and an unreadable one would look
    /// like another tokenizer and cost every data source of this provider its index.
    /// </remarks>
    [Parameter]
    public string DataTokenizerFingerprint { get; set; } = string.Empty;

    [Parameter]
    public int DataTokenLimit { get; set; } = EmbeddingProvider.DEFAULT_TOKEN_LIMIT;

    [Parameter]
    public int DataEmbeddingBatchSize { get; set; } = EmbeddingProvider.DEFAULT_EMBEDDING_BATCH_SIZE;

    /// <summary>
    /// Whether this embedding provider is managed by an enterprise configuration plugin. When true,
    /// every field except the API key is locked, matching Settings.EmbeddingProvider.IsEnterpriseConfiguration.
    /// </summary>
    [Parameter]
    public bool IsEnterpriseConfiguration { get; set; }

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<EmbeddingProviderDialog> Logger { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    /// <summary>
    /// The list of used instance names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedInstanceNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataAPIKey = string.Empty;
    private bool dataHadStoredAPIKeyOnLoad;
    private string dataAPIKeyStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    private string dataLoadingModelsIssue = string.Empty;
    private bool dataConfiguredModelIsNotOffered;
    private string dataFilePath = string.Empty;
    private string dataTokenizerFingerprint = string.Empty;
    private string dataCustomTokenizerValidationIssue = string.Empty;
    private Task dataTokenizerValidationTask = Task.CompletedTask;
    private bool dataStoreWasAttempted;
    private bool isTokenizerFileDialogOpen;
    private bool showExpertSettings;
    private int dataTokenizerValidationRevision;

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private readonly List<Model> availableModels = new();
    private readonly Encryption encryption = Program.ENCRYPTION;
    private readonly ProviderValidation providerValidation;

    public EmbeddingProviderDialog()
    {
        this.providerValidation = new()
        {
            GetProvider = () => this.DataLLMProvider,
            GetAPIKeyStorageIssue = () => this.dataAPIKeyStorageIssue,
            GetPreviousInstanceName = () => this.dataEditingPreviousInstanceName,
            GetUsedInstanceNames = () => this.UsedInstanceNames,
            GetHost = () => this.DataHost,
            GetCustomTokenizerValidationIssue = () => this.dataCustomTokenizerValidationIssue,
        };
    }
    
    private EmbeddingProvider CreateEmbeddingProviderSettings()
    {
        var cleanedHostname = this.DataHostname.Trim();
        return new()
        {
            Num = this.DataNum,
            Id = this.DataId,
            Name = this.DataName,
            UsedLLMProvider = this.DataLLMProvider,
            Model = this.DataModel,
            IsSelfHosted = this.DataLLMProvider is LLMProviders.SELF_HOSTED,
            Hostname = cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname,
            Host = this.DataHost,
            IsEnterpriseConfiguration = this.IsEnterpriseConfiguration,
            EnterpriseConfigurationPluginId = Guid.Empty,
            TokenizerPath = this.dataFilePath,
            TokenizerFingerprint = this.dataTokenizerFingerprint,
            EmbeddingBatchSize = this.DataEmbeddingBatchSize,
            TokenLimit = this.DataTokenLimit,
            CustomIconDataUrl = this.DataCustomIconDataUrl,
            HFInferenceProvider = this.HFInferenceProviderId,
        };
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Call the base initialization first so that the I18N is ready:
        await base.OnInitializedAsync();
        
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedInstanceNames = this.SettingsManager.ConfigurationData.EmbeddingProviders.Select(x => x.Name.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataName.ToLowerInvariant();
            this.dataFilePath = this.DataTokenizerPath;
            this.dataTokenizerFingerprint = this.DataTokenizerFingerprint;
            this.showExpertSettings = !string.IsNullOrWhiteSpace(this.DataTokenizerPath)
                                      || this.DataTokenLimit != EmbeddingProvider.DEFAULT_TOKEN_LIMIT
                                      || this.DataEmbeddingBatchSize != EmbeddingProvider.DEFAULT_EMBEDDING_BATCH_SIZE;
            
            // Load the API key. A self-hosted server may well need one: LM Studio can ask for a
            // token of its own, and any of these servers can sit behind an authenticating proxy.
            // So we try for every host and treat a missing key as the normal case (isTrying).
            // ReloadModels() below reads dataAPIKey, so the key has to be here before it runs:
            var requestedSecret = await this.RustService.GetAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER, isTrying: this.DataLLMProvider is LLMProviders.SELF_HOSTED);
            if (requestedSecret.Success)
            {
                this.dataAPIKey = await requestedSecret.Secret.Decrypt(this.encryption);
                this.dataHadStoredAPIKeyOnLoad = !string.IsNullOrWhiteSpace(this.dataAPIKey);
            }
            else
            {
                this.dataAPIKey = string.Empty;

                // For an enterprise-managed provider, having no key yet is the expected first-run
                // state, not a storage failure -- the user is just about to set their own key:
                if (this.DataLLMProvider is not LLMProviders.SELF_HOSTED && !this.IsEnterpriseConfiguration)
                {
                    this.dataAPIKeyStorageIssue = string.Format(T("Failed to load the API key from the operating system. The message was: {0}. You might ignore this message and provide the API key again."), requestedSecret.Issue);
                    await this.form.Validate();
                }
            }

            await this.ReloadModels();
        }
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

    // Must mirror Settings.EmbeddingProvider.SecretId exactly: when editing an enterprise-managed
    // provider, the key has to be stored under the same "ENT::"-prefixed keyring row that the
    // app reads from at runtime (see BaseProvider.SecretId). Otherwise, a key entered here would
    // silently end up in the wrong keyring row and never be found again.
    public string SecretId => this.IsEnterpriseConfiguration ? $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{this.DataLLMProvider.ToSecretId()}" : this.DataLLMProvider.ToSecretId();

    public string SecretName => this.DataName;

    #endregion
    
    private async Task Store()
    {
        this.dataStoreWasAttempted = true;
        await this.dataTokenizerValidationTask;
        await this.form.Validate();
        this.dataAPIKeyStorageIssue = string.Empty;

        // Manually validate the model selection (needed when no models are loaded
        // and the MudSelect is not rendered):
        var modelValidationError = this.providerValidation.ValidatingModel(this.DataModel);
        if (!string.IsNullOrWhiteSpace(modelValidationError))
        {
            this.dataIssues = [..this.dataIssues, modelValidationError];
            this.dataIsValid = false;
        }

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        //
        // Ask before anything is written. Storing a tokenizer deletes the previous one before it
        // copies, and the API key goes into the OS keyring right after, so asking any later would
        // leave those changes behind even when the user says no. Saying no also keeps this dialog
        // open, which is the point: the value which would have cost the index can be corrected
        // right away.
        //
        // Enterprise-managed providers are left out. Every field which reaches the embedding
        // signature is locked for them, and their data sources are not queued for indexing either.
        //
        if (this.IsEditing && !this.IsEnterpriseConfiguration && !await DataSourceReindexWarning.ConfirmEmbeddingProviderChangeAsync(
                this.DialogService, this.SettingsManager, this.DataSourceEmbeddingService,
                this.SettingsManager.GetEmbeddingProviderById(this.DataId), this.CreateEmbeddingProviderSettings()))
            return;

        var response = await this.StoreOrDeleteTokenizerAsync();
        if (!response.Success)
        {
            this.dataCustomTokenizerValidationIssue = string.IsNullOrWhiteSpace(response.Message) ? string.Empty : response.Message;
            await this.form.Validate();
            return;
        }
        this.dataFilePath = response.StoredPath;
        
        // Use the data model to store the provider.
        // We just return this data to the parent component:
        var addedProviderSettings = this.CreateEmbeddingProviderSettings();
        if (!string.IsNullOrWhiteSpace(this.dataAPIKey))
        {
            // Store the API key in the OS secure storage:
            var storeResponse = await this.RustService.SetAPIKey(this, this.dataAPIKey, SecretStoreType.EMBEDDING_PROVIDER);
            if (!storeResponse.Success)
            {
                this.dataAPIKeyStorageIssue = string.Format(T("Failed to store the API key in the operating system. The message was: {0}. Please try again."), storeResponse.Issue);
                await this.form.Validate();
                return;
            }

            this.dataHadStoredAPIKeyOnLoad = true;
        }
        else if (this.dataHadStoredAPIKeyOnLoad)
        {
            // The user cleared a previously stored key. Without this, the old key would simply
            // stay in the OS keyring untouched and keep being used:
            var deleteResponse = await this.RustService.DeleteAPIKey(this, SecretStoreType.EMBEDDING_PROVIDER);
            if (!deleteResponse.Success)
            {
                this.dataAPIKeyStorageIssue = string.Format(T("Failed to remove the API key from the operating system. The message was: {0}. Please try again."), deleteResponse.Issue);
                await this.form.Validate();
                return;
            }

            this.dataHadStoredAPIKeyOnLoad = false;
        }

        this.MudDialog.Close(DialogResult.Ok(addedProviderSettings));
    }
    
    private string? ValidateTokenLimit(int tokenLimit)
    {
        if (tokenLimit < 1)
            return T("Please enter a token limit greater than 0.");

        return null;
    }

    private string? ValidateEmbeddingBatchSize(int embeddingBatchSize)
    {
        if (embeddingBatchSize < 1)
            return T("Please enter an embedding batch size greater than 0.");

        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();

    private async Task OnAPIKeyChanged(string apiKey)
    {
        this.dataAPIKey = apiKey;
        if (!string.IsNullOrWhiteSpace(this.dataAPIKeyStorageIssue))
        {
            this.dataAPIKeyStorageIssue = string.Empty;
            await this.form.Validate();
        }
    }

    private async Task OpenTokenizerFileDialog()
    {
        if (this.isTokenizerFileDialogOpen)
            return;

        this.isTokenizerFileDialogOpen = true;
        try
        {
            var response = await this.RustService.SelectFile(T("Choose a custom tokenizer here"), [ FileTypes.JSON ], string.IsNullOrWhiteSpace(this.dataFilePath) ? null : this.dataFilePath);
            if (!response.UserCancelled)
                await this.OnDataFilePathChanged(response.SelectedFilePath);
        }
        finally
        {
            this.isTokenizerFileDialogOpen = false;
        }
    }

    private Task ClearPathTokenizer(MouseEventArgs _)
    {
        return this.OnDataFilePathChanged(string.Empty);
    }

    /// <summary>
    /// Takes the first dropped path which can serve as a tokenizer.
    /// </summary>
    /// <remarks>
    /// A provider carries exactly one tokenizer, so a multi-selection cannot be honored as a whole.
    /// Everything which is not a readable JSON file is skipped rather than handed to the runtime:
    /// the validation would reject it anyway, and saying so right away names the actual mistake.
    /// </remarks>
    /// <param name="paths">The dropped paths.</param>
    private async Task OnTokenizerPathsDropped(List<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path) || !FileTypes.IsAllowedPath(path, FileTypes.JSON))
                continue;

            await this.OnDataFilePathChanged(path);
            return;
        }

        this.Logger.LogWarning("None of the {Count} dropped path(s) could be used as a tokenizer.", paths.Count);
        await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, T("Please drop a tokenizer file in the JSON format.")));
    }

    private async Task OnDataFilePathChanged(string filePath)
    {
        this.dataFilePath = filePath;
        var validationRevision = ++this.dataTokenizerValidationRevision;
        this.dataTokenizerValidationTask = this.ValidateCustomTokenizer(filePath, validationRevision);
        await this.dataTokenizerValidationTask;

        //
        // The embedding signature carries the tokenizer's content, so it has to be read while we have
        // the file the user just picked. Reading it here rather than while storing also keeps a large
        // file off that path, where it would stall the circuit.
        //
        var tokenizerFingerprint = await TokenizerFingerprint.ForFileAsync(filePath);

        if (validationRevision != this.dataTokenizerValidationRevision)
            return;

        this.dataTokenizerFingerprint = tokenizerFingerprint;

        if (this.dataStoreWasAttempted)
            await this.form.Validate();
        else
            this.form.ResetValidation();
    }

    private async Task ValidateCustomTokenizer(string filePath, int validationRevision)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (validationRevision == this.dataTokenizerValidationRevision)
                this.dataCustomTokenizerValidationIssue = string.Empty;

            return;
        }

        try
        {
            var response = await this.RustService.ValidateTokenizer(filePath);
            if (validationRevision != this.dataTokenizerValidationRevision)
                return;

            if (response.Success)
                this.dataCustomTokenizerValidationIssue = string.Empty;
            else
                this.dataCustomTokenizerValidationIssue = T("Invalid tokenizer: ") + response.Message;
        }
        catch (Exception e)
        {
            if (validationRevision != this.dataTokenizerValidationRevision)
                return;

            this.Logger.LogError(e, "Failed to validate custom tokenizer.");
            this.dataCustomTokenizerValidationIssue = T("Failed to validate the selected tokenizer. Please try again.");
        }
    }

    /// <summary>
    /// Stores a new tokenizer or deletes the existing one, based on the specified tokenizer path.
    /// If the path is null or empty, any existing tokenizer is removed.
    /// Otherwise, the tokenizer is stored at the specified path.
    /// </summary>
    private Task<TokenizerResponse> StoreOrDeleteTokenizerAsync()
    {
        var tokenizerId = TokenizerModelId.ForEmbeddingProviderId(this.DataId);
        if (string.IsNullOrWhiteSpace(this.dataFilePath))
            return this.RustService.DeleteTokenizer(tokenizerId);

        return this.RustService.StoreTokenizer(tokenizerId, this.dataFilePath);
    }

    private void OnHostChanged(Host selectedHost)
    {
        // When the host changes, reset the model selection state:
        this.DataHost = selectedHost;
        this.DataModel = default;
        this.availableModels.Clear();
        this.dataLoadingModelsIssue = string.Empty;
        this.dataConfiguredModelIsNotOffered = false;
    }

    /// <summary>
    /// Resets the model selection when the user picks another Hugging Face inference provider.
    /// </summary>
    /// <remarks>
    /// Each inference provider offers embedding models of its own, so the models loaded for the
    /// previous one say nothing about the new one.
    /// </remarks>
    /// <param name="selectedInferenceProvider">The inference provider the user chose.</param>
    private void OnHFInferenceProviderChanged(HFInferenceProvider selectedInferenceProvider)
    {
        this.HFInferenceProviderId = selectedInferenceProvider;
        this.DataModel = default;
        this.availableModels.Clear();
        this.dataLoadingModelsIssue = string.Empty;
        this.dataConfiguredModelIsNotOffered = false;
    }

    private async Task ReloadModels()
    {
        this.dataLoadingModelsIssue = string.Empty;
        var currentEmbeddingProviderSettings = this.CreateEmbeddingProviderSettings();
        var provider = currentEmbeddingProviderSettings.CreateProvider();
        if (provider is NoProvider)
            return;

        try
        {
            var result = await provider.GetEmbeddingModels(this.dataAPIKey);
            if (!result.Success)
                this.dataLoadingModelsIssue = result.FailureReason.ToUserMessage(provider.InstanceName);

            // Order descending by ID means that the newest models probably come first:
            var orderedModels = result.Models.OrderByDescending(n => n.Id);

            this.availableModels.Clear();
            this.availableModels.AddRange(orderedModels);
        }
        catch (Exception e)
        {
            this.Logger.LogError($"Failed to load models from provider '{this.DataLLMProvider}' (host={this.DataHost}, hostname='{this.DataHostname}'): {e.Message}");
            this.dataLoadingModelsIssue = T("We are currently unable to communicate with the provider to load models. Please try again later.");
        }

        // Whatever the server answered, and whether it answered at all, the model this provider was
        // configured with stays on the list:
        this.PinConfiguredModel();
    }

    /// <summary>
    /// Keeps the configured model selectable, also when the server does not offer it right now.
    /// </summary>
    /// <remarks>
    /// This is deliberately the opposite of what the chat provider dialog does, which replaces the
    /// configured model with the one the server reported. An embedding provider carries indexed data
    /// sources, and its model ID is part of the embedding signature: changing it -- even only in its
    /// spelling -- means every prepared document is prepared again. So the stored model is added to
    /// the list here rather than the list being applied to the stored model. A model nobody serves
    /// any more stays visible and stays chosen, and changing it stays the user's decision, which
    /// storing then asks about.
    ///
    /// Comparing is what Model does, which is by ID and ordinal. Matching a differing spelling would
    /// mean writing that other spelling into the settings, and that is the very change this avoids.
    /// </remarks>
    private void PinConfiguredModel()
    {
        if (string.IsNullOrWhiteSpace(this.DataModel.Id))
        {
            this.dataConfiguredModelIsNotOffered = false;
            return;
        }

        this.dataConfiguredModelIsNotOffered = !this.availableModels.Contains(this.DataModel);
        if (this.dataConfiguredModelIsNotOffered)
            this.availableModels.Insert(0, this.DataModel);
    }

    private string APIKeyText => this.DataLLMProvider switch
    {
        LLMProviders.SELF_HOSTED => T("(Optional) API Key"),
        _ => T("API Key"),
    };
    
    private bool IsNoneProvider => this.DataLLMProvider is LLMProviders.NONE;

    private void ToggleExpertSettings() => this.showExpertSettings = !this.showExpertSettings;

    private string GetExpertStyles => this.showExpertSettings ? "border-2 border-dashed rounded pa-2" : string.Empty;
}
