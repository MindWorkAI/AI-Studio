using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Finds what the user still has to do after a restore.
/// </summary>
/// <remarks>
/// A backup holds no secrets, so the providers, data sources, and mailboxes of the user lack them on
/// another computer. Those of the organization do not: their configuration plugin brings them along
/// with their secrets. The files and folders which the settings name may be missing, too, wherever
/// they come from.
/// </remarks>
public static class DataRestoreOpenPoints
{
    /// <summary>
    /// Finds what the user still has to do after a restore.
    /// </summary>
    /// <param name="settings">The restored settings.</param>
    /// <param name="secrets">Tells which secrets this computer holds.</param>
    /// <returns>The open points, in the order of the settings.</returns>
    public static async Task<IReadOnlyList<DataRestoreOpenPoint>> CollectAsync(Data settings, IStoredSecrets secrets)
    {
        var openPoints = new List<DataRestoreOpenPoint>();
        foreach (var provider in settings.Providers.Where(provider => !provider.IsEnterpriseConfiguration && provider.UsedLLMProvider.IsAPIKeyNeeded(provider.Host)))
            if (!await secrets.HasAPIKeyAsync(provider, SecretStoreType.LLM_PROVIDER))
                openPoints.Add(new(DataRestoreOpenPointKind.PROVIDER_WITHOUT_API_KEY, provider.InstanceName));

        foreach (var provider in settings.EmbeddingProviders.Where(provider => !provider.IsEnterpriseConfiguration && provider.UsedLLMProvider.IsAPIKeyNeeded(provider.Host)))
            if (!await secrets.HasAPIKeyAsync(provider, SecretStoreType.EMBEDDING_PROVIDER))
                openPoints.Add(new(DataRestoreOpenPointKind.EMBEDDING_PROVIDER_WITHOUT_API_KEY, provider.Name));

        foreach (var provider in settings.TranscriptionProviders.Where(provider => !provider.IsEnterpriseConfiguration && provider.UsedLLMProvider.IsAPIKeyNeeded(provider.Host)))
            if (!await secrets.HasAPIKeyAsync(provider, SecretStoreType.TRANSCRIPTION_PROVIDER))
                openPoints.Add(new(DataRestoreOpenPointKind.TRANSCRIPTION_PROVIDER_WITHOUT_API_KEY, provider.Name));

        foreach (var dataSource in settings.DataSources)
        {
            switch (dataSource)
            {
                case DataSourceLocalDirectory directory when !Directory.Exists(directory.Path):
                    openPoints.Add(new(DataRestoreOpenPointKind.DATA_SOURCE_NOT_FOUND, directory.Name));
                    break;

                case DataSourceLocalFile file when !File.Exists(file.FilePath):
                    openPoints.Add(new(DataRestoreOpenPointKind.DATA_SOURCE_NOT_FOUND, file.Name));
                    break;

                case IERIDataSource { IsEnterpriseConfiguration: false, AuthMethod: AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD } eriDataSource:
                    if (!await secrets.HasSecretAsync(eriDataSource, SecretStoreType.DATA_SOURCE))
                        openPoints.Add(new(DataRestoreOpenPointKind.DATA_SOURCE_WITHOUT_SECRET, eriDataSource.Name));

                    break;
            }
        }

        foreach (var mailbox in settings.Mailboxes.Where(mailbox => mailbox is { IsEnterpriseConfiguration: false, AuthMethod: MailboxAuthMethod.PASSWORD }))
            if (!await secrets.HasSecretAsync(mailbox, SecretStoreType.DATA_SOURCE))
                openPoints.Add(new(DataRestoreOpenPointKind.MAILBOX_WITHOUT_PASSWORD, mailbox.Name));

        var batchProcessing = settings.BatchProcessing;
        foreach (var folder in new[] { batchProcessing.InputDirectory, batchProcessing.OutputDirectory })
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                openPoints.Add(new(DataRestoreOpenPointKind.BATCH_PROCESSING_PATH_NOT_FOUND, folder));

        if (!string.IsNullOrWhiteSpace(batchProcessing.PromptFilePath) && !File.Exists(batchProcessing.PromptFilePath))
            openPoints.Add(new(DataRestoreOpenPointKind.BATCH_PROCESSING_PATH_NOT_FOUND, batchProcessing.PromptFilePath));

        var certificateBundlePath = settings.App.ExternalHttpCustomRootCertificateBundlePath;
        if (settings.App.ExternalHttpCustomRootCertificatesEnabled && !string.IsNullOrWhiteSpace(certificateBundlePath) && !File.Exists(certificateBundlePath))
            openPoints.Add(new(DataRestoreOpenPointKind.CERTIFICATE_BUNDLE_NOT_FOUND, certificateBundlePath));

        foreach (var server in settings.ERI.ERIServers.Where(server => server.WriteToFilesystem && !string.IsNullOrWhiteSpace(server.BaseDirectory) && !Directory.Exists(server.BaseDirectory)))
            openPoints.Add(new(DataRestoreOpenPointKind.ERI_SERVER_DIRECTORY_NOT_FOUND, server.ServerName));

        return openPoints;
    }
}