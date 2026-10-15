using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.ERIClient.DataModel;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks what the user hears to do after a restore.
/// </summary>
/// <remarks>
/// A backup holds no secrets, so whatever of the user needs one lacks it on the new computer. The
/// objects of the organization come with their secrets from the configuration plugin, and naming them
/// would send the user hunting for keys they never had.
/// </remarks>
[TestFixture]
public sealed class DataRestoreOpenPointsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("4f1c2b7a-8d3e-4a6b-9c5d-2e7f1a3b5c8d");

    private string testDirectory = string.Empty;

    [SetUp]
    public void CreateFolder()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-restore-open-points-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(this.testDirectory))
            Directory.Delete(this.testDirectory, true);
    }

    [Test]
    public async Task TheMissingSecretsOfTheUserAreOpenPoints()
    {
        var settings = new Data();
        settings.Providers.Add(new ProviderSettings(1, "user-provider", "My OpenAI", LLMProviders.OPEN_AI, new("gpt-5", "gpt-5")));
        settings.Providers.Add(new ProviderSettings(2, "stored-provider", "My Anthropic", LLMProviders.ANTHROPIC, new("claude", "claude")));
        settings.Providers.Add(new ProviderSettings(3, "plugin-provider", "OpenAI of the organization", LLMProviders.OPEN_AI, new("gpt-5", "gpt-5"), IsEnterpriseConfiguration: true, EnterpriseConfigurationPluginId: PLUGIN_ID));
        settings.EmbeddingProviders.Add(new EmbeddingProvider(1, "user-embedding", "My embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small")));
        settings.TranscriptionProviders.Add(new TranscriptionProvider(1, "user-transcription", "My transcription", LLMProviders.OPEN_AI, new("whisper-1", "whisper-1")));
        settings.DataSources.Add(new DataSourceERI_V1 { Id = "token-server", Name = "My ERI server", AuthMethod = AuthMethod.TOKEN });
        settings.DataSources.Add(new DataSourceERI_V1 { Id = "open-server", Name = "Open ERI server", AuthMethod = AuthMethod.NONE });
        settings.DataSources.Add(new DataSourceERI_V1 { Id = "plugin-server", Name = "ERI server of the organization", AuthMethod = AuthMethod.TOKEN, IsEnterpriseConfiguration = true, EnterpriseConfigurationPluginId = PLUGIN_ID });
        settings.Mailboxes.Add(new DataSourceMailbox { Id = "user-mailbox", Name = "My mailbox", AuthMethod = MailboxAuthMethod.PASSWORD });

        var openPoints = await DataRestoreOpenPoints.CollectAsync(settings, new StoredSecrets("My Anthropic"), []);

        Assert.That(openPoints, Is.EqualTo(new DataRestoreOpenPoint[]
        {
            new(DataRestoreOpenPointKind.PROVIDER_WITHOUT_API_KEY, "My OpenAI"),
            new(DataRestoreOpenPointKind.EMBEDDING_PROVIDER_WITHOUT_API_KEY, "My embeddings"),
            new(DataRestoreOpenPointKind.TRANSCRIPTION_PROVIDER_WITHOUT_API_KEY, "My transcription"),
            new(DataRestoreOpenPointKind.DATA_SOURCE_WITHOUT_SECRET, "My ERI server"),
            new(DataRestoreOpenPointKind.MAILBOX_WITHOUT_PASSWORD, "My mailbox"),
        }));
    }

    [Test]
    public async Task TheFilesAndFoldersMissingOnThisComputerAreOpenPoints()
    {
        var missingFolder = Path.Combine(this.testDirectory, "missing");
        var missingFile = Path.Combine(this.testDirectory, "missing.txt");
        var settings = new Data();
        settings.DataSources.Add(new DataSourceLocalDirectory { Id = "missing-directory", Name = "Projects", Path = missingFolder });
        settings.DataSources.Add(new DataSourceLocalDirectory { Id = "existing-directory", Name = "Notes", Path = this.testDirectory });
        settings.DataSources.Add(new DataSourceLocalFile { Id = "missing-file", Name = "Handbook", FilePath = missingFile });
        settings.BatchProcessing.InputDirectory = missingFolder;
        settings.BatchProcessing.OutputDirectory = this.testDirectory;
        settings.BatchProcessing.PromptFilePath = missingFile;
        settings.App.ExternalHttpCustomRootCertificatesEnabled = true;
        settings.App.ExternalHttpCustomRootCertificateBundlePath = missingFile;
        settings.ERI.ERIServers.Add(new DataERIServer { ServerName = "Wiki server", WriteToFilesystem = true, BaseDirectory = missingFolder });
        settings.ERI.ERIServers.Add(new DataERIServer { ServerName = "Draft server", WriteToFilesystem = false, BaseDirectory = missingFolder });

        var openPoints = await DataRestoreOpenPoints.CollectAsync(settings, new StoredSecrets(), []);

        Assert.That(openPoints, Is.EqualTo(new DataRestoreOpenPoint[]
        {
            new(DataRestoreOpenPointKind.DATA_SOURCE_NOT_FOUND, "Projects"),
            new(DataRestoreOpenPointKind.DATA_SOURCE_NOT_FOUND, "Handbook"),
            new(DataRestoreOpenPointKind.BATCH_PROCESSING_PATH_NOT_FOUND, missingFolder),
            new(DataRestoreOpenPointKind.BATCH_PROCESSING_PATH_NOT_FOUND, missingFile),
            new(DataRestoreOpenPointKind.CERTIFICATE_BUNDLE_NOT_FOUND, missingFile),
            new(DataRestoreOpenPointKind.ERI_SERVER_DIRECTORY_NOT_FOUND, "Wiki server"),
        }));
    }

    [Test]
    public async Task AnUnusedCertificateBundleIsNoOpenPoint()
    {
        var settings = new Data();
        settings.App.ExternalHttpCustomRootCertificatesEnabled = false;
        settings.App.ExternalHttpCustomRootCertificateBundlePath = Path.Combine(this.testDirectory, "missing.pem");

        var openPoints = await DataRestoreOpenPoints.CollectAsync(settings, new StoredSecrets(), []);

        Assert.That(openPoints, Is.Empty);
    }

    [Test]
    public async Task TheMissingSecretsOfTheToolsAreOpenPoints()
    {
        DataRestoreToolSecret[] toolSecrets =
        [
            new("web_search", "tavily.apiKey", "Web search", "Tavily API Key"),
            new("web_search", "staan.apiKey", "Web search", "Staan API Key"),
        ];

        var openPoints = await DataRestoreOpenPoints.CollectAsync(new Data(), new StoredSecrets("staan.apiKey"), toolSecrets);

        Assert.That(openPoints, Is.EqualTo(new DataRestoreOpenPoint[] { new(DataRestoreOpenPointKind.TOOL_WITHOUT_SECRET, "Web search", "Tavily API Key") }));
    }

    /// <summary>
    /// A keyring which holds the secrets of the given names.
    /// </summary>
    private sealed class StoredSecrets(params string[] storedSecretNames) : IStoredSecrets
    {
        public Task<bool> HasAPIKeyAsync(ISecretId secretId, SecretStoreType storeType) => Task.FromResult(storedSecretNames.Contains(secretId.SecretName));

        public Task<bool> HasSecretAsync(ISecretId secretId, SecretStoreType storeType) => Task.FromResult(storedSecretNames.Contains(secretId.SecretName));
    }
}