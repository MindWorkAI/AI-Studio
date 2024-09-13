using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;

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
    private ILogger<Settings> Logger { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    private readonly List<ConfigurationSelectData<string>> availableProviders = new();

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

    #region Provider related

    private async Task AddProvider()
    {
        var dialogParameters = new DialogParameters<ProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Add Provider", dialogParameters, DialogOptions.FULLSCREEN);
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

    private async Task EditProvider(AIStudio.Settings.Provider provider)
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

        var dialogReference = await this.DialogService.ShowAsync<ProviderDialog>("Edit Provider", dialogParameters, DialogOptions.FULLSCREEN);
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

    private async Task DeleteProvider(AIStudio.Settings.Provider provider)
    {
        var dialogParameters = new DialogParameters
        {
            { "Message", $"Are you sure you want to delete the provider '{provider.InstanceName}'?" },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Provider", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var providerInstance = provider.CreateProvider(this.Logger);
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(providerInstance);
        if(deleteSecretResponse.Success)
        {
            this.SettingsManager.ConfigurationData.Providers.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        
        this.UpdateProviders();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private bool HasDashboard(LLMProviders llmProvider) => llmProvider switch
    {
        LLMProviders.OPEN_AI => true,
        LLMProviders.MISTRAL => true,
        LLMProviders.ANTHROPIC => true,
        LLMProviders.FIREWORKS => true,
        
        _ => false,
    };
    
    private string GetProviderDashboardURL(LLMProviders llmProvider) => llmProvider switch
    {
        LLMProviders.OPEN_AI => "https://platform.openai.com/usage",
        LLMProviders.MISTRAL => "https://console.mistral.ai/usage/",
        LLMProviders.ANTHROPIC => "https://console.anthropic.com/settings/plans",
        LLMProviders.FIREWORKS => "https://fireworks.ai/account/billing",
        
        _ => string.Empty,
    };

    private string GetProviderModelName(AIStudio.Settings.Provider provider)
    {
        const int MAX_LENGTH = 36;
        var modelName = provider.Model.ToString();
        return modelName.Length > MAX_LENGTH ? "[...] " + modelName[^Math.Min(MAX_LENGTH, modelName.Length)..] : modelName;
    }
    
    private void UpdateProviders()
    {
        this.availableProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
            this.availableProviders.Add(new (provider.InstanceName, provider.Id));
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
            return $"background-color: {level.GetColor()};";

        return $"background-color: {ConfidenceLevel.UNKNOWN.GetColor()};";
    }

    private async Task ChangeCustomConfidenceLevel(LLMProviders llmProvider, ConfidenceLevel level)
    {
        this.SettingsManager.ConfigurationData.LLMProviders.CustomConfidenceScheme[llmProvider] = level;
        await this.SettingsManager.StoreSettings();
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