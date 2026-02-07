using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelTranscription : SettingsPanelBase
{
    [Parameter]
    public List<ConfigurationSelectData<string>> AvailableTranscriptionProviders { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<ConfigurationSelectData<string>>> AvailableTranscriptionProvidersChanged { get; set; }
    
    private string GetTranscriptionProviderModelName(TranscriptionProvider provider)
    {
        // For system models, return localized text:
        if (provider.Model.IsSystemModel)
            return T("Uses the provider-configured model");

        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.UpdateTranscriptionProviders();
        await base.OnInitializedAsync();
    }

    #endregion
    
    private async Task AddTranscriptionProvider()
    {
        var dialogParameters = new DialogParameters<TranscriptionProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<TranscriptionProviderDialog>(T("Add Transcription Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var addedTranscription = (TranscriptionProvider)dialogResult.Data!;
        addedTranscription = addedTranscription with { Num = this.SettingsManager.ConfigurationData.NextTranscriptionNum++ };
        
        this.SettingsManager.ConfigurationData.TranscriptionProviders.Add(addedTranscription);
        await this.UpdateTranscriptionProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditTranscriptionProvider(TranscriptionProvider transcriptionProvider)
    {
        var dialogParameters = new DialogParameters<TranscriptionProviderDialog>
        {
            { x => x.DataNum, transcriptionProvider.Num },
            { x => x.DataId, transcriptionProvider.Id },
            { x => x.DataName, transcriptionProvider.Name },
            { x => x.DataLLMProvider, transcriptionProvider.UsedLLMProvider },
            { x => x.DataModel, transcriptionProvider.Model },
            { x => x.DataHostname, transcriptionProvider.Hostname },
            { x => x.IsSelfHosted, transcriptionProvider.IsSelfHosted },
            { x => x.IsEditing, true },
            { x => x.DataHost, transcriptionProvider.Host },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<TranscriptionProviderDialog>(T("Edit Transcription Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var editedTranscriptionProvider = (TranscriptionProvider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedTranscriptionProvider.Num == 0)
            editedTranscriptionProvider = editedTranscriptionProvider with { Num = this.SettingsManager.ConfigurationData.NextTranscriptionNum++ };
        
        this.SettingsManager.ConfigurationData.TranscriptionProviders[this.SettingsManager.ConfigurationData.TranscriptionProviders.IndexOf(transcriptionProvider)] = editedTranscriptionProvider;
        await this.UpdateTranscriptionProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteTranscriptionProvider(TranscriptionProvider provider)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the transcription provider '{0}'?"), provider.Name) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Transcription Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider, SecretStoreType.TRANSCRIPTION_PROVIDER);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.TranscriptionProviders.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }

        await this.UpdateTranscriptionProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task ExportTranscriptionProvider(TranscriptionProvider provider)
    {
        if (provider == TranscriptionProvider.NONE)
            return;

        string? encryptedApiKey = null;

        // Check if the provider has an API key stored:
        var apiKeyResponse = await this.RustService.GetAPIKey(provider, SecretStoreType.TRANSCRIPTION_PROVIDER, isTrying: true);
        if (apiKeyResponse.Success)
        {
            // Ask the user if they want to export the API key:
            var dialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, T("This provider has an API key configured. Do you want to include the encrypted API key in the export? Note: The recipient will need the same encryption secret to use the API key.") },
            };

            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Export API Key?"), dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult is { Canceled: false })
            {
                // User wants to export the API key - encrypt it:
                var encryption = PluginFactory.EnterpriseEncryption;
                if (encryption?.IsAvailable == true)
                {
                    var decryptedApiKey = await apiKeyResponse.Secret.Decrypt(Program.ENCRYPTION);
                    if (encryption.TryEncrypt(decryptedApiKey, out var encrypted))
                        encryptedApiKey = encrypted;
                }
                else
                {
                    // No encryption secret available - inform the user:
                    this.Snackbar.Add(T("Cannot export the encrypted API key: No enterprise encryption secret is configured."), Severity.Warning);
                }
            }
        }

        var luaCode = provider.ExportAsConfigurationSection(encryptedApiKey);
        if (string.IsNullOrWhiteSpace(luaCode))
            return;

        await this.RustService.CopyText2Clipboard(this.Snackbar, luaCode);
    }
    
    private async Task UpdateTranscriptionProviders()
    {
        this.AvailableTranscriptionProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.TranscriptionProviders)
            this.AvailableTranscriptionProviders.Add(new (provider.Name, provider.Id));
        
        await this.AvailableTranscriptionProvidersChanged.InvokeAsync(this.AvailableTranscriptionProviders);
    }
}
