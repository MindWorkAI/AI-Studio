using System.Text.RegularExpressions;

using AIStudio.Provider;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

namespace AIStudio.Settings;

/// <summary>
/// The provider settings dialog.
/// </summary>
public partial class ProviderDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
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
    /// The provider to use.
    /// </summary>
    [Parameter]
    public Providers DataProvider { get; set; } = Providers.NONE;
    
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
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    /// <summary>
    /// The list of used instance names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedInstanceNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataAPIKey = string.Empty;
    private string dataAPIKeyStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    
    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;
    
    private readonly List<Model> availableModels = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Load the used instance names:
        this.UsedInstanceNames = this.SettingsManager.ConfigurationData.Providers.Select(x => x.InstanceName.ToLowerInvariant()).ToList();
        
        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataInstanceName.ToLowerInvariant();
            var provider = this.DataProvider.CreateProvider(this.DataInstanceName);
            if(provider is NoProvider)
                return;
            
            // Load the API key:
            var requestedSecret = await this.SettingsManager.GetAPIKey(this.JsRuntime, provider);
            if(requestedSecret.Success)
            {
                this.dataAPIKey = requestedSecret.Secret;
                
                // Now, we try to load the list of available models:
                await this.ReloadModels();
            }
            else
            {
                this.dataAPIKeyStorageIssue = $"Failed to load the API key from the operating system. The message was: {requestedSecret.Issue}. You might ignore this message and provide the API key again.";
                await this.form.Validate();
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
        var addedProvider = new Provider
        {
            Id = this.DataId,
            InstanceName = this.DataInstanceName,
            UsedProvider = this.DataProvider,
            Model = this.DataModel,
        };
        
        // We need to instantiate the provider to store the API key:
        var provider = this.DataProvider.CreateProvider(this.DataInstanceName);
            
        // Store the API key in the OS secure storage:
        var storeResponse = await this.SettingsManager.SetAPIKey(this.JsRuntime, provider, this.dataAPIKey);
        if (!storeResponse.Success)
        {
            this.dataAPIKeyStorageIssue = $"Failed to store the API key in the operating system. The message was: {storeResponse.Issue}. Please try again.";
            await this.form.Validate();
            return;
        }
        
        this.MudDialog.Close(DialogResult.Ok(addedProvider));
    }
    
    private string? ValidatingProvider(Providers provider)
    {
        if (provider == Providers.NONE)
            return "Please select a provider.";
        
        return null;
    }
    
    private string? ValidatingModel(Model model)
    {
        if (model == default)
            return "Please select a model.";
        
        return null;
    }
    
    [GeneratedRegex("^[a-zA-Z0-9 ]+$")]
    private static partial Regex InstanceNameRegex();
    
    private string? ValidatingInstanceName(string instanceName)
    {
        if(string.IsNullOrWhiteSpace(instanceName))
            return "Please enter an instance name.";
        
        if(instanceName.StartsWith(' '))
            return "The instance name must not start with a space.";
        
        if(instanceName.EndsWith(' '))
            return "The instance name must not end with a space.";
        
        // The instance name must only contain letters, numbers, and spaces:
        if (!InstanceNameRegex().IsMatch(instanceName))
            return "The instance name must only contain letters, numbers, and spaces.";
        
        if(instanceName.Contains("  "))
            return "The instance name must not contain consecutive spaces.";
        
        // The instance name must be unique:
        var lowerInstanceName = instanceName.ToLowerInvariant();
        if (lowerInstanceName != this.dataEditingPreviousInstanceName && this.UsedInstanceNames.Contains(lowerInstanceName))
            return "The instance name must be unique; the chosen name is already in use.";
        
        return null;
    }
    
    private string? ValidatingAPIKey(string apiKey)
    {
        if(!string.IsNullOrWhiteSpace(this.dataAPIKeyStorageIssue))
            return this.dataAPIKeyStorageIssue;

        if(string.IsNullOrWhiteSpace(apiKey))
            return "Please enter an API key.";
        
        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();

    private bool CanLoadModels => !string.IsNullOrWhiteSpace(this.dataAPIKey) && this.DataProvider != Providers.NONE && !string.IsNullOrWhiteSpace(this.DataInstanceName);
    
    private async Task ReloadModels()
    {
        var provider = this.DataProvider.CreateProvider(this.DataInstanceName);
        if(provider is NoProvider)
            return;

        var models = await provider.GetTextModels(this.JsRuntime, this.SettingsManager);
        
        // Order descending by ID means that the newest models probably come first:
        var orderedModels = models.OrderByDescending(n => n.Id);
        
        this.availableModels.Clear();
        this.availableModels.AddRange(orderedModels);
    }
}