using System.Diagnostics.CodeAnalysis;

using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelProviders : SettingsPanelBase
{
    [Parameter]
    public List<ConfigurationSelectData<string>> AvailableLLMProviders { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<ConfigurationSelectData<string>>> AvailableLLMProvidersChanged { get; set; }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.UpdateProviders();
        await base.OnInitializedAsync();
    }

    #endregion
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private async Task AddLLMProvider()
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>(T("Add LLM Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        addedProvider = addedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers.Add(addedProvider);
        await this.UpdateProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private async Task EditLLMProvider(AIStudio.Settings.Provider provider)
    {
        if (provider.IsEnterpriseConfiguration)
            return;
        
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.DataNum, provider.Num },
            { x => x.DataId, provider.Id },
            { x => x.DataInstanceName, provider.InstanceName },
            { x => x.DataLLMProvider, provider.UsedLLMProvider },
            { x => x.DataModel, provider.Model },
            { x => x.DataHostname, provider.Hostname },
            { x => x.IsSelfHosted, provider.IsSelfHosted },
            { x => x.IsEditing, true },
            { x => x.DataHost, provider.Host },
            { x => x.HFInferenceProviderId, provider.HFInferenceProvider },
        };

        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>(T("Edit LLM Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedProvider.Num == 0)
            editedProvider = editedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers[this.SettingsManager.ConfigurationData.Providers.IndexOf(provider)] = editedProvider;
        await this.UpdateProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private async Task DeleteLLMProvider(AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", string.Format(T("Are you sure you want to delete the provider '{0}'?"), provider.InstanceName) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete LLM Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        else
        {
            var issueDialogParameters = new DialogParameters
            {
                { "Message", string.Format(T("Couldn't delete the provider '{0}'. The issue: {1}. We can ignore this issue and delete the provider anyway. Do you want to ignore it and delete this provider?"), provider.InstanceName, deleteSecretResponse.Issue) },
            };
        
            var issueDialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete LLM Provider"), issueDialogParameters, DialogOptions.FULLSCREEN);
            var issueDialogResult = await issueDialogReference.Result;
            if (issueDialogResult is null || issueDialogResult.Canceled)
                return;
            
            // Case: The user wants to ignore the issue and delete the provider anyway:
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }

        await this.UpdateProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private string GetLLMProviderModelName(AIStudio.Settings.Provider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private async Task UpdateProviders()
    {
        this.AvailableLLMProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            this.AvailableLLMProviders.Add(new (provider.InstanceName, provider.Id));
        
        await this.AvailableLLMProvidersChanged.InvokeAsync(this.AvailableLLMProviders);
    }

    private string GetCurrentConfidenceLevelName(LLMProviders llmProvider)
    {
        if (this.SettingsManager.ConfigurationData.LLMProviders.CustomConfidenceScheme.TryGetValue(llmProvider, out var level))
            return level.GetName();

        return T("Not yet configured");
    }
    
    private string SetCurrentConfidenceLevelColorStyle(LLMProviders llmProvider)
    {
        if (this.SettingsManager.ConfigurationData.LLMProviders.CustomConfidenceScheme.TryGetValue(llmProvider, out var level))
            return $"background-color: {level.GetColor(this.SettingsManager)};";

        return $"background-color: {ConfidenceLevel.UNKNOWN.GetColor(this.SettingsManager)};";
    }

    private async Task ChangeCustomConfidenceLevel(LLMProviders llmProvider, ConfidenceLevel level)
    {
        this.SettingsManager.ConfigurationData.LLMProviders.CustomConfidenceScheme[llmProvider] = level;
        await this.SettingsManager.StoreSettings();
    }
}