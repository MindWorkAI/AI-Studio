using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;
using RustService = AIStudio.Tools.RustService;

// ReSharper disable ClassNeverInstantiated.Global

namespace AIStudio.Pages;

public partial class Settings : ComponentBase, IMessageBusReceiver, IDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    private readonly List<ConfigurationSelectData<string>> availableLLMProviders = new();
    private readonly List<ConfigurationSelectData<string>> availableEmbeddingProviders = new();

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        this.UpdateProviders();
        await base.OnInitializedAsync();
    }

    #endregion

    #region Preview-feature related

    private void UpdatePreviewFeatures(PreviewVisibility previewVisibility)
    {
        this.SettingsManager.ConfigurationData.App.PreviewVisibility = previewVisibility;
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures = previewVisibility.FilterPreviewFeatures(this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures);
    }

    #endregion
    
    #region Provider related

    private async Task AddLLMProvider()
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Add LLM Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        addedProvider = addedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers.Add(addedProvider);
        this.UpdateProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task EditLLMProvider(AIStudio.Settings.Provider provider)
    {
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
        };

        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Edit LLM Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedProvider = (AIStudio.Settings.Provider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedProvider.Num == 0)
            editedProvider = editedProvider with { Num = this.SettingsManager.ConfigurationData.NextProviderNum++ };
        
        this.SettingsManager.ConfigurationData.Providers[this.SettingsManager.ConfigurationData.Providers.IndexOf(provider)] = editedProvider;
        this.UpdateProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteLLMProvider(AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the provider '{provider.InstanceName}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete LLM Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        
        this.UpdateProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private string GetLLMProviderModelName(AIStudio.Settings.Provider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }
    
    private void UpdateProviders()
    {
        this.availableLLMProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            this.availableLLMProviders.Add(new (provider.InstanceName, provider.Id));
    }

    private string GetCurrentConfidenceLevelName(LLMProviders llmProvider)
    {
        if (this.SettingsManager.ConfigurationData.LLMProviders.CustomConfidenceScheme.TryGetValue(llmProvider, out var level))
            return level.GetName();

        return "Not yet configured";
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

    #endregion

    #region Embedding provider related 

    private string GetEmbeddingProviderModelName(EmbeddingProvider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }
    
    private async Task AddEmbeddingProvider()
    {
        var dialogParameters = new DialogParameters<EmbeddingDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<EmbeddingDialog>("Add Embedding Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedEmbedding = (EmbeddingProvider)dialogResult.Data!;
        addedEmbedding = addedEmbedding with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders.Add(addedEmbedding);
        this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditEmbeddingProvider(EmbeddingProvider embeddingProvider)
    {
        var dialogParameters = new DialogParameters<EmbeddingDialog>
        {
            { x => x.DataNum, embeddingProvider.Num },
            { x => x.DataId, embeddingProvider.Id },
            { x => x.DataName, embeddingProvider.Name },
            { x => x.DataLLMProvider, embeddingProvider.UsedLLMProvider },
            { x => x.DataModel, embeddingProvider.Model },
            { x => x.DataHostname, embeddingProvider.Hostname },
            { x => x.IsSelfHosted, embeddingProvider.IsSelfHosted },
            { x => x.IsEditing, true },
            { x => x.DataHost, embeddingProvider.Host },
        };

        var dialogReference = await this.DialogService.ShowAsync<EmbeddingDialog>("Edit Embedding Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedEmbeddingProvider = (EmbeddingProvider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedEmbeddingProvider.Num == 0)
            editedEmbeddingProvider = editedEmbeddingProvider with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders[this.SettingsManager.ConfigurationData.EmbeddingProviders.IndexOf(embeddingProvider)] = editedEmbeddingProvider;
        this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteEmbeddingProvider(EmbeddingProvider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the embedding provider '{provider.Name}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Embedding Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.EmbeddingProviders.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        
        this.UpdateEmbeddingProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private void UpdateEmbeddingProviders()
    {
        this.availableEmbeddingProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.EmbeddingProviders)
            this.availableEmbeddingProviders.Add(new (provider.Name, provider.Id));
    }

    #endregion

    #region Profile related

    private async Task AddProfile()
    {
        var dialogParameters = new DialogParameters<ProfileDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProfileDialog>("Add Profile", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var addedProfile = (Profile)dialogResult.Data!;
        addedProfile = addedProfile with { Num = this.SettingsManager.ConfigurationData.NextProfileNum++ };
        
        this.SettingsManager.ConfigurationData.Profiles.Add(addedProfile);
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditProfile(Profile profile)
    {
        var dialogParameters = new DialogParameters<ProfileDialog>
        {
            { x => x.DataNum, profile.Num },
            { x => x.DataId, profile.Id },
            { x => x.DataName, profile.Name },
            { x => x.DataNeedToKnow, profile.NeedToKnow },
            { x => x.DataActions, profile.Actions },
            { x => x.IsEditing, true },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProfileDialog>("Edit Profile", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var editedProfile = (Profile)dialogResult.Data!;
        this.SettingsManager.ConfigurationData.Profiles[this.SettingsManager.ConfigurationData.Profiles.IndexOf(profile)] = editedProfile;
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteProfile(Profile profile)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the profile '{profile.Name}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Profile", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.Profiles.Remove(profile);
        await this.SettingsManager.StoreSettings();
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    #endregion

    #region Bias-of-the-day related

    private async Task ResetBiasOfTheDayHistory()
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", "Are you sure you want to reset your bias-of-the-day statistics? The system will no longer remember which biases you already know. As a result, biases you are already familiar with may be addressed again." },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Reset your bias-of-the-day statistics", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        this.SettingsManager.ConfigurationData.BiasOfTheDay.UsedBias.Clear();
        this.SettingsManager.ConfigurationData.BiasOfTheDay.DateLastBiasDrawn = DateOnly.MinValue;
        await this.SettingsManager.StoreSettings();
        
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    #endregion
    
    #region Implementation of IMessageBusReceiver

    public Task ProcessMessage<TMsg>(ComponentBase? sendingComponent, Event triggeredEvent, TMsg? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
}