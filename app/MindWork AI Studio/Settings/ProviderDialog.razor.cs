using System.Text.RegularExpressions;

using AIStudio.Provider;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

namespace AIStudio.Settings;

public partial class ProviderDialog : ComponentBase
{
    [CascadingParameter]
    private MudDialogInstance MudDialog { get; set; } = null!;
    
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();
    
    [Parameter]
    public string DataInstanceName { get; set; } = string.Empty;
    
    [Parameter]
    public Providers DataProvider { get; set; } = Providers.NONE;

    [Parameter]
    public bool IsEditing { get; init; }
    
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    private List<string> usedInstanceNames { get; set; } = [];
    
    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataAPIKey = string.Empty;
    private string dataAPIKeyStorageIssue = string.Empty;
    private string dataEditingPreviousInstanceName = string.Empty;
    
    private MudForm form = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.usedInstanceNames = this.SettingsManager.ConfigurationData.Providers.Select(x => x.InstanceName.ToLowerInvariant()).ToList();
        
        if(this.IsEditing)
        {
            this.dataEditingPreviousInstanceName = this.DataInstanceName.ToLowerInvariant();
            var provider = this.DataProvider.CreateProvider();
            if(provider is NoProvider)
                return;
            
            provider.InstanceName = this.DataInstanceName;
            
            var requestedSecret = await this.SettingsManager.GetAPIKey(this.JsRuntime, provider);
            if(requestedSecret.Success)
                this.dataAPIKey = requestedSecret.Secret;
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
        
        if (!this.dataIsValid)
            return;
        
        var addedProvider = new Provider
        {
            Id = this.DataId,
            InstanceName = this.DataInstanceName,
            UsedProvider = this.DataProvider,
        };
        
        var provider = this.DataProvider.CreateProvider();
        provider.InstanceName = this.DataInstanceName;
            
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
        if (lowerInstanceName != this.dataEditingPreviousInstanceName && this.usedInstanceNames.Contains(lowerInstanceName))
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
}