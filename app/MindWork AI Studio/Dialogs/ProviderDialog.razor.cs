using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Validation;

using Microsoft.AspNetCore.Components;

using Host = AIStudio.Provider.SelfHosted.Host;
using RustService = AIStudio.Tools.RustService;

namespace AIStudio.Dialogs;

/// <summary>
/// The provider settings dialog.
/// </summary>
public partial class ProviderDialog : ComponentBase, ISecretId
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The provider's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }
    
    /// <summary>
    /// The provider's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The user chosen instance name.
    /// </summary>
    [Parameter]
    public string DataInstanceName { get; set; } = string.Empty;
    
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
    /// The LLM model to use, e.g., GPT-4o.
    /// </summary>
    [Parameter]
    public Model DataModel { get; set; }
    
    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    private ILogger<ProviderDialog> Logger { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    /// <summary>
    /// The list of used instance names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedInstanceNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataAPIKey = string.Empty;
    private string dataManuallyModel = string.Empty;
    private string dataAPIKeyStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private readonly List<Model> availableModels = new();
    private readonly Encryption encryption = Program.ENCRYPTION;
    private readonly ProviderValidation providerValidation;

    public ProviderDialog()
    {
        this.providerValidation = new()
        {
            GetProvider = () => this.DataLLMProvider,
            GetAPIKeyStorageIssue = () => this.dataAPIKeyStorageIssue,
            GetPreviousInstanceName = () => this.dataEditingPreviousInstanceName,
            GetUsedInstanceNames = () => this.UsedInstanceNames,
            GetHost = () => this.DataHost,
        };
    }

    private AIStudio.Settings.Provider CreateProviderSettings()
    {
        var cleanedHostname = this.DataHostname.Trim();
        return new()
        {
            Num = this.DataNum,
            Id = this.DataId,
            InstanceName = this.DataInstanceName,
            UsedLLMProvider = this.DataLLMProvider,
            Model = this.DataLLMProvider is LLMProviders.FIREWORKS ? new Model(this.dataManuallyModel, null) : this.DataModel,
            IsSelfHosted = this.DataLLMProvider is LLMProviders.SELF_HOSTED,
            Hostname = cleanedHostname.EndsWith('/') ? cleanedHostname[..^1] : cleanedHostname,
            Host = this.DataHost,
        };
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        // Load the used instance names:
        this.UsedInstanceNames = this.SettingsManager.ConfigurationData.Providers.Select(x => x.InstanceName.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataInstanceName.ToLowerInvariant();
            
            // When using Fireworks, we must copy the model name:
            if (this.DataLLMProvider is LLMProviders.FIREWORKS)
                this.dataManuallyModel = this.DataModel.Id;
            
            //
            // We cannot load the API key for self-hosted providers:
            //
            if (this.DataLLMProvider is LLMProviders.SELF_HOSTED && this.DataHost is not Host.OLLAMA)
            {
                await this.ReloadModels();
                await base.OnInitializedAsync();
                return;
            }
            
            // Load the API key:
            var requestedSecret = await this.RustService.GetAPIKey(this, isTrying: this.DataLLMProvider is LLMProviders.SELF_HOSTED);
            if (requestedSecret.Success)
                this.dataAPIKey = await requestedSecret.Secret.Decrypt(this.encryption);
            else
            {
                this.dataAPIKey = string.Empty;
                if (this.DataLLMProvider is not LLMProviders.SELF_HOSTED)
                {
                    this.dataAPIKeyStorageIssue = $"Failed to load the API key from the operating system. The message was: {requestedSecret.Issue}. You might ignore this message and provide the API key again.";
                    await this.form.Validate();
                }
            }

            await this.ReloadModels();
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

    public string SecretId => this.DataLLMProvider.ToName();
    
    public string SecretName => this.DataInstanceName;

    #endregion

    private async Task Store()
    {
        await this.form.Validate();
        if (!string.IsNullOrWhiteSpace(this.dataAPIKeyStorageIssue))
            this.dataAPIKeyStorageIssue = string.Empty;
        
        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;
        
        // Use the data model to store the provider.
        // We just return this data to the parent component:
        var addedProviderSettings = this.CreateProviderSettings();
        if (!string.IsNullOrWhiteSpace(this.dataAPIKey))
        {
            // Store the API key in the OS secure storage:
            var storeResponse = await this.RustService.SetAPIKey(this, this.dataAPIKey);
            if (!storeResponse.Success)
            {
                this.dataAPIKeyStorageIssue = $"Failed to store the API key in the operating system. The message was: {storeResponse.Issue}. Please try again.";
                await this.form.Validate();
                return;
            }
        }

        this.MudDialog.Close(DialogResult.Ok(addedProviderSettings));
    }
    
    private string? ValidateManuallyModel(string manuallyModel)
    {
        if (this.DataLLMProvider is LLMProviders.FIREWORKS && string.IsNullOrWhiteSpace(manuallyModel))
            return "Please enter a model name.";
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
    
    private async Task ReloadModels()
    {
        var currentProviderSettings = this.CreateProviderSettings();
        var provider = currentProviderSettings.CreateProvider(this.Logger);
        if(provider is NoProvider)
            return;
        
        var models = await provider.GetTextModels(this.dataAPIKey);
        
        // Order descending by ID means that the newest models probably come first:
        var orderedModels = models.OrderByDescending(n => n.Id);
        
        this.availableModels.Clear();
        this.availableModels.AddRange(orderedModels);
    }
    
    private string APIKeyText => this.DataLLMProvider switch
    {
        LLMProviders.SELF_HOSTED => "(Optional) API Key",
        _ => "API Key",
    };
    
    private bool IsNoneProvider => this.DataLLMProvider is LLMProviders.NONE;
}