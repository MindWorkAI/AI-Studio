using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.DataBackup;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks the settings as a backup takes them along.
/// </summary>
/// <remarks>
/// A configuration plugin brings its objects back on another computer, along with their API keys.
/// Restored from a backup as well, they would compete with what the plugin brings, and without the
/// plugin they would stay without their keys, since secrets never go into a backup. What the user
/// configured themselves has to come along, though, and the settings AI Studio runs with must not
/// lose anything while a backup is made.
/// </remarks>
[TestFixture]
public sealed class DataBackupSettingsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("7b2d4f6a-8c1e-4a3b-9d5f-2e4a6c8b0d1f");

    [Test]
    public void EveryObjectOfAConfigurationPluginIsLeftOut()
    {
        var settings = CreateSettingsWithObjectsOfTheUserAndOfAPlugin();

        var copy = DataBackupSettings.CopyWithoutPluginObjects(settings);

        Assert.Multiple(() =>
        {
            Assert.That(copy.Providers.Select(provider => provider.Id), Is.EqualTo(new[] { "user-provider" }));
            Assert.That(copy.TranscriptionProviders.Select(provider => provider.Id), Is.EqualTo(new[] { "user-transcription" }));
            Assert.That(copy.EmbeddingProviders.Select(provider => provider.Id), Is.EqualTo(new[] { "user-embedding" }));
            Assert.That(copy.ChatTemplates.Select(template => template.Id), Is.EqualTo(new[] { "user-template" }));
            Assert.That(copy.DataSources.Select(dataSource => dataSource.Id), Is.EqualTo(new[] { "user-data-source" }));
            Assert.That(copy.Profiles.Select(profile => profile.Id), Is.EqualTo(new[] { "user-profile" }));
            Assert.That(copy.DocumentAnalysis.Policies.Select(policy => policy.Id), Is.EqualTo(new[] { "user-policy" }));
        });
    }

    [Test]
    public void TheSettingsAIStudioRunsWithStayAsTheyAre()
    {
        var settings = CreateSettingsWithObjectsOfTheUserAndOfAPlugin();

        DataBackupSettings.CopyWithoutPluginObjects(settings);

        Assert.Multiple(() =>
        {
            Assert.That(settings.Providers, Has.Count.EqualTo(2));
            Assert.That(settings.DataSources, Has.Count.EqualTo(2));
            Assert.That(settings.DocumentAnalysis.Policies, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void ALockedSettingStaysWithItsPlugin()
    {
        var settings = CreateSettingsWithObjectsOfTheUserAndOfAPlugin();
        settings.ManagedLockedConfigurations["App.UpdateInterval"] = PLUGIN_ID;
        settings.ManagedUserValueSnapshots["App.UpdateInterval"] = "\"CHECK_EVERY_DAY\"";

        var copy = DataBackupSettings.CopyWithoutPluginObjects(settings);

        Assert.Multiple(() =>
        {
            Assert.That(copy.ManagedLockedConfigurations["App.UpdateInterval"], Is.EqualTo(PLUGIN_ID), "After a restore, the clean-up hands the setting back to the user unless the plugin locks it again.");
            Assert.That(copy.ManagedUserValueSnapshots["App.UpdateInterval"], Is.EqualTo("\"CHECK_EVERY_DAY\""), "That is the value the user gets back.");
        });
    }

    private static Data CreateSettingsWithObjectsOfTheUserAndOfAPlugin()
    {
        var settings = new Data();

        settings.Providers.Add(new AIStudio.Settings.Provider(1, "user-provider", "My provider", LLMProviders.OPEN_AI, new("gpt-6", "gpt-6")));
        settings.Providers.Add(new AIStudio.Settings.Provider(2, "plugin-provider", "Provider of the organization", LLMProviders.SELF_HOSTED, new("llama", "llama"), IsSelfHosted: true, IsEnterpriseConfiguration: true, EnterpriseConfigurationPluginId: PLUGIN_ID));

        settings.TranscriptionProviders.Add(new TranscriptionProvider(1, "user-transcription", "My transcription", LLMProviders.OPEN_AI, new("whisper-1", "whisper-1")));
        settings.TranscriptionProviders.Add(new TranscriptionProvider(2, "plugin-transcription", "Transcription of the organization", LLMProviders.OPEN_AI, new("whisper-1", "whisper-1"), IsEnterpriseConfiguration: true, EnterpriseConfigurationPluginId: PLUGIN_ID));

        settings.EmbeddingProviders.Add(new EmbeddingProvider(1, "user-embedding", "My embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small")));
        settings.EmbeddingProviders.Add(new EmbeddingProvider(2, "plugin-embedding", "Embeddings of the organization", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"), IsEnterpriseConfiguration: true, EnterpriseConfigurationPluginId: PLUGIN_ID));

        settings.ChatTemplates.Add(new ChatTemplate { Id = "user-template", Name = "My template" });
        settings.ChatTemplates.Add(new ChatTemplate { Id = "plugin-template", Name = "Template of the organization", IsEnterpriseConfiguration = true, EnterpriseConfigurationPluginId = PLUGIN_ID });

        settings.DataSources.Add(new DataSourceERI_V1 { Id = "user-data-source", Name = "My server" });
        settings.DataSources.Add(new DataSourceERI_V1 { Id = "plugin-data-source", Name = "Server of the organization", IsEnterpriseConfiguration = true, EnterpriseConfigurationPluginId = PLUGIN_ID });

        settings.Profiles.Add(new Profile { Id = "user-profile", Name = "My profile" });
        settings.Profiles.Add(new Profile { Id = "plugin-profile", Name = "Profile of the organization", IsEnterpriseConfiguration = true, EnterpriseConfigurationPluginId = PLUGIN_ID });

        settings.DocumentAnalysis.Policies.Add(new DataDocumentAnalysisPolicy { Id = "user-policy", Name = "My policy" });
        settings.DocumentAnalysis.Policies.Add(new DataDocumentAnalysisPolicy { Id = "plugin-policy", Name = "Policy of the organization", IsEnterpriseConfiguration = true, EnterpriseConfigurationPluginId = PLUGIN_ID });

        return settings;
    }
}