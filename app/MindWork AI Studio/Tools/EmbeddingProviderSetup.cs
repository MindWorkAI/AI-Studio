using AIStudio.Dialogs;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Tools;

/// <summary>
/// Adds an embedding provider, either from scratch or from an imported configuration, and tells
/// whether the user may do so.
/// </summary>
/// <remarks>
/// Kept here rather than in the places which offer it -- the embedding provider panel and the data
/// source settings, which offer it while no embedding provider exists -- so that adding one works the
/// same way everywhere: the organization's permission is checked, the provider is stored, and the
/// data sources are checked against it.
/// </remarks>
public static class EmbeddingProviderSetup
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(EmbeddingProviderSetup).Namespace, nameof(EmbeddingProviderSetup));

    /// <summary>
    /// Whether the organization lets the user add an embedding provider.
    /// </summary>
    public static bool MayAdd(SettingsManager settingsManager) => settingsManager.ConfigurationData.App.AllowUserToAddProvider && settingsManager.ConfigurationData.App.AllowUserToAddEmbeddingProvider;

    /// <summary>
    /// Whether the organization lets the user import an embedding provider.
    /// </summary>
    /// <remarks>
    /// An import adds a provider, so this holds only when MayAdd holds as well.
    /// </remarks>
    public static bool MayImport(SettingsManager settingsManager) => settingsManager.ConfigurationData.App.CanImportConfigurationSnippet(PluginConfigurationObjectType.EMBEDDING_PROVIDER);

    /// <summary>
    /// Whether no embedding provider exists, so that no local data source can be prepared for the AI.
    /// </summary>
    public static bool IsMissing(SettingsManager settingsManager) => settingsManager.ConfigurationData.EmbeddingProviders.Count == 0;

    /// <summary>
    /// The parameters which open the embedding provider dialog with the given provider filled in.
    /// </summary>
    /// <param name="embeddingProvider">The provider to fill in.</param>
    /// <param name="isEditing">True to edit this provider, false to add it as a new one, as an import does.</param>
    public static DialogParameters<EmbeddingProviderDialog> CreateDialogParameters(EmbeddingProvider embeddingProvider, bool isEditing) => new()
    {
        { x => x.DataNum, embeddingProvider.Num },
        { x => x.DataId, embeddingProvider.Id },
        { x => x.DataName, embeddingProvider.Name },
        { x => x.DataLLMProvider, embeddingProvider.UsedLLMProvider },
        { x => x.DataCustomIconDataUrl, embeddingProvider.CustomIconDataUrl },
        { x => x.DataModel, embeddingProvider.Model },
        { x => x.DataHostname, embeddingProvider.Hostname },
        { x => x.IsSelfHosted, embeddingProvider.IsSelfHosted },
        { x => x.IsEditing, isEditing },
        { x => x.DataHost, embeddingProvider.Host },
        { x => x.DataTokenizerPath, embeddingProvider.TokenizerPath },
        { x => x.DataTokenizerFingerprint, embeddingProvider.TokenizerFingerprint },
        { x => x.DataTokenLimit, embeddingProvider.EffectiveTokenLimit },
        { x => x.DataEmbeddingBatchSize, embeddingProvider.EffectiveEmbeddingBatchSize },
        { x => x.HFInferenceProviderId, embeddingProvider.HFInferenceProvider },
        { x => x.IsEnterpriseConfiguration, embeddingProvider.IsEnterpriseConfiguration },
    };

    /// <summary>
    /// Lets the user add an embedding provider from scratch, and stores it when they save.
    /// </summary>
    /// <remarks>
    /// The caller tells the rest of the app that the configuration changed, as the sender of that message.
    /// </remarks>
    /// <param name="dialogService">The dialog service to show the provider settings with.</param>
    /// <param name="settingsManager">The settings, which receive the provider.</param>
    /// <param name="embeddingService">The service which checks the data sources against the new provider.</param>
    /// <returns>The added provider, or null when the user cancelled or the organization does not allow adding one.</returns>
    public static async Task<EmbeddingProvider?> AddAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService)
    {
        if (!MayAdd(settingsManager))
            return null;

        var dialogParameters = new DialogParameters<EmbeddingProviderDialog>
        {
            { x => x.IsEditing, false },
        };

        return await AddFromDialogAsync(dialogService, settingsManager, embeddingService, dialogParameters, isImport: false);
    }

    /// <summary>
    /// Lets the user import an embedding provider from a configuration snippet, and stores it when they save.
    /// </summary>
    /// <remarks>
    /// The caller tells the rest of the app that the configuration changed, as the sender of that message.
    /// </remarks>
    /// <param name="dialogService">The dialog service to show the import and the provider settings with.</param>
    /// <param name="settingsManager">The settings, which receive the provider.</param>
    /// <param name="embeddingService">The service which checks the data sources against the new provider.</param>
    /// <returns>The imported provider, or null when the user cancelled or the organization does not allow importing one.</returns>
    public static async Task<EmbeddingProvider?> ImportAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService)
    {
        if (!MayImport(settingsManager))
            return null;

        var import = await ConfigurationSnippetImportDialog.ShowAsync(dialogService, PluginConfigurationObjectType.EMBEDDING_PROVIDER, TB("Import Embedding"));
        if (import?.ConfigurationObject is not EmbeddingProvider embeddingProvider)
            return null;

        var dialogParameters = CreateDialogParameters(embeddingProvider, isEditing: false);
        dialogParameters.Add(x => x.ImportedAPIKey, import.Secret);
        return await AddFromDialogAsync(dialogService, settingsManager, embeddingService, dialogParameters, isImport: true);
    }

    private static async Task<EmbeddingProvider?> AddFromDialogAsync(IDialogService dialogService, SettingsManager settingsManager, DataSourceEmbeddingService embeddingService, DialogParameters<EmbeddingProviderDialog> dialogParameters, bool isImport)
    {
        var dialogReference = await dialogService.ShowAsync<EmbeddingProviderDialog>(TB("Add Embedding Provider"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return null;

        //
        // An organization can lock the import while the user is still filling in the form, e.g.,
        // with a new version of its configuration plugin. Hence, the permission is checked once
        // more before the imported embedding provider is stored:
        //
        if (isImport && !MayImport(settingsManager))
            return null;

        var addedEmbedding = (EmbeddingProvider)dialogResult.Data!;
        addedEmbedding = addedEmbedding with { Num = settingsManager.ConfigurationData.NextEmbeddingNum++ };

        settingsManager.ConfigurationData.EmbeddingProviders.Add(addedEmbedding);
        await settingsManager.StoreSettings();
        await embeddingService.QueueAllInternalDataSourcesAsync();
        return addedEmbedding;
    }
}