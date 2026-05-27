using System.Globalization;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Services;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components.Settings;

public partial class SettingsPanelEmbeddings : SettingsPanelProviderBase
{
    [Inject]
    private DataSourceEmbeddingService DataSourceEmbeddingService { get; init; } = null!;

    [Parameter]
    public List<ConfigurationSelectData<string>> AvailableEmbeddingProviders { get; set; } = new();
    
    [Parameter]
    public EventCallback<List<ConfigurationSelectData<string>>> AvailableEmbeddingProvidersChanged { get; set; }
    
    private string GetEmbeddingProviderModelName(EmbeddingProvider provider)
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
        await this.UpdateEmbeddingProviders();
        await base.OnInitializedAsync();
    }

    #endregion

    private async Task AddEmbeddingProvider()
    {
        var dialogParameters = new DialogParameters<EmbeddingProviderDialog>
        {
            { x => x.IsEditing, false },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<EmbeddingProviderDialog>(T("Add Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var addedEmbedding = (EmbeddingProvider)dialogResult.Data!;
        addedEmbedding = addedEmbedding with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders.Add(addedEmbedding);
        await this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueAllInternalDataSourcesAsync();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private async Task EditEmbeddingProvider(EmbeddingProvider embeddingProvider)
    {
        var dialogParameters = new DialogParameters<EmbeddingProviderDialog>
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
            { x => x.DataTokenizerPath, embeddingProvider.TokenizerPath },
        };

        var dialogReference = await this.DialogService.ShowAsync<EmbeddingProviderDialog>(T("Edit Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var editedEmbeddingProvider = (EmbeddingProvider)dialogResult.Data!;
        
        // Set the provider number if it's not set. This is important for providers
        // added before we started saving the provider number.
        if(editedEmbeddingProvider.Num == 0)
            editedEmbeddingProvider = editedEmbeddingProvider with { Num = this.SettingsManager.ConfigurationData.NextEmbeddingNum++ };
        
        this.SettingsManager.ConfigurationData.EmbeddingProviders[this.SettingsManager.ConfigurationData.EmbeddingProviders.IndexOf(embeddingProvider)] = editedEmbeddingProvider;
        await this.UpdateEmbeddingProviders();
        
        await this.SettingsManager.StoreSettings();
        await this.DataSourceEmbeddingService.QueueAllInternalDataSourcesAsync();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task DeleteEmbeddingProvider(EmbeddingProvider provider)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Message, string.Format(T("Are you sure you want to delete the embedding provider '{0}'?"), provider.Name) },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;
        
        var deleteSecretResponse = await this.RustService.DeleteAPIKey(provider, SecretStoreType.EMBEDDING_PROVIDER);
        var deleteTokenizerResponse = await this.RustService.DeleteTokenizer(TokenizerModelId.ForEmbeddingProvider(provider));
        if(deleteSecretResponse.Success && deleteTokenizerResponse.Success)
        {
            this.SettingsManager.ConfigurationData.EmbeddingProviders.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }
        else
        {
            var issueDialogParameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Message, string.Format(T("Couldn't delete the embedding provider '{0}'. The issue: {1}. We can ignore this issue and delete the embedding provider anyway. Do you want to ignore it and delete this embedding provider?"), provider.Name, BuildDeleteIssue(deleteSecretResponse, deleteTokenizerResponse)) },
            };

            var issueDialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete Embedding Provider"), issueDialogParameters, DialogOptions.FULLSCREEN);
            var issueDialogResult = await issueDialogReference.Result;
            if (issueDialogResult is null || issueDialogResult.Canceled)
                return;

            this.SettingsManager.ConfigurationData.EmbeddingProviders.Remove(provider);
            await this.SettingsManager.StoreSettings();
        }

        await this.UpdateEmbeddingProviders();
        await this.DataSourceEmbeddingService.QueueAllInternalDataSourcesAsync();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private static string BuildDeleteIssue(DeleteSecretResponse deleteSecretResponse, TokenizerResponse deleteTokenizerResponse)
    {
        var issues = new List<string>();
        if (!deleteSecretResponse.Success)
            issues.Add(deleteSecretResponse.Issue);

        if (!deleteTokenizerResponse.Success)
            issues.Add(deleteTokenizerResponse.Message);

        return string.Join(" | ", issues);
    }

    private async Task ExportEmbeddingProvider(EmbeddingProvider provider)
    {
        if (!this.SettingsManager.ConfigurationData.App.ShowAdminSettings)
            return;

        if (provider == EmbeddingProvider.NONE)
            return;

        await this.ExportProvider(provider, SecretStoreType.EMBEDDING_PROVIDER, provider.ExportAsConfigurationSection);
    }
    
    private async Task UpdateEmbeddingProviders()
    {
        this.AvailableEmbeddingProviders.Clear();
        foreach (var provider in this.SettingsManager.ConfigurationData.EmbeddingProviders)
            this.AvailableEmbeddingProviders.Add(new (provider.Name, provider.Id));
        
        await this.AvailableEmbeddingProvidersChanged.InvokeAsync(this.AvailableEmbeddingProviders);
    }

    private async Task TestEmbeddingProvider(EmbeddingProvider provider)
    {
        var dialogParameters = new DialogParameters<SingleInputDialog>
        {
            { x => x.ConfirmText, T("Embed text") },
            { x => x.InputHeaderText, T("Add text that should be embedded:") },
            { x => x.UserInput, T("Example text to embed") },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<SingleInputDialog>(T("Test Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return;

        var inputText = dialogResult.Data as string;
        if (string.IsNullOrWhiteSpace(inputText))
            return;

        var embeddingProvider = provider.CreateProvider();
        var embeddings = await embeddingProvider.EmbedTextAsync(provider.Model, this.SettingsManager, CancellationToken.None, inputText);

        if (embeddings.Count == 0)
        {
            await this.DialogService.ShowMessageBox(T("Embedding Result"), T("No embedding was returned."), T("Close"));
            return;
        }

        var vector = embeddings.FirstOrDefault();
        if (vector is null || vector.Count == 0)
        {
            await this.DialogService.ShowMessageBox(T("Embedding Result"), T("No embedding was returned."), T("Close"));
            return;
        }

        var resultText = string.Join(Environment.NewLine, vector.Select(value => value.ToString("G9", CultureInfo.InvariantCulture)));
        var resultParameters = new DialogParameters<EmbeddingResultDialog>
        {
            { x => x.ResultText, resultText },
            { x => x.ResultLabel, T("Embedding Vector (one dimension per line)") },
        };

        await this.DialogService.ShowAsync<EmbeddingResultDialog>(T("Embedding Result"), resultParameters, DialogOptions.FULLSCREEN);
    }
}
