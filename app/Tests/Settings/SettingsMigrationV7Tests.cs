using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

using Version = AIStudio.Settings.Version;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the migration of the settings to version V7, which took the word "agent" out of the settings
/// of the AI tasks.
/// </summary>
/// <remarks>
/// The keys of the managed settings name a class and a property, so they change as well. Without the
/// migration, the clean-up of the managed settings would drop every lock, organization default, and
/// snapshot of the user's value stored under a former name, because no setting is registered under it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class SettingsMigrationV7Tests
{
    private const string SETTINGS_FILENAME = "settings.json";

    private static readonly Guid CONFIG_PLUGIN_ID = Guid.Parse("8f5f1a3e-2b44-4c55-9e1d-0a6b7c8d9e01");

    private static readonly string V6_SETTINGS = $$"""
        {
          "Version": "V6",
          "TextContentCleaner": { "PreselectAgentOptions": true, "PreselectedAgentProvider": "cleaner", "MinimumProviderConfidence": "HIGH" },
          "AgentDataSourceSelection": { "PreselectAgentOptions": true, "PreselectedAgentProvider": "selection", "MinimumProviderConfidence": "MEDIUM" },
          "AgentRetrievalContextValidation": { "EnableRetrievalContextValidation": true, "PreselectAgentOptions": true, "PreselectedAgentProvider": "validation", "MinimumProviderConfidence": "LOW", "NumParallelValidations": 7 },
          "AssistantPluginAudit": { "RequireAuditBeforeActivation": false, "PreselectedAgentProvider": "audit", "MinimumProviderConfidence": "MODERATE" },
          "Translation": { "PreselectContentCleanerAgent": true, "PreselectedProvider": "translation" },
          "TextSummarizer": { "PreselectContentCleanerAgent": true, "PreselectedProvider": "summarizer" },
          "SWOTAnalysis": { "PreselectContentCleanerAgent": true, "PreselectedProvider": "swot" },
          "LegalCheck": { "PreselectContentCleanerAgent": true, "PreselectedProvider": "legal" },
          "ManagedLockedConfigurations": {
            "DataAssistantPluginAudit.PreselectedAgentProvider": "{{CONFIG_PLUGIN_ID}}",
            "DataApp.UpdateInterval": "{{CONFIG_PLUGIN_ID}}"
          },
          "ManagedEditableDefaults": {
            "DataAgentDataSourceSelection.PreselectedAgentProvider": { "ConfigPluginId": "{{CONFIG_PLUGIN_ID}}", "LastAppliedValue": "selection" }
          },
          "ManagedUserValueSnapshots": {
            "DataAgentRetrievalContextValidation.NumParallelValidations": "5"
          }
        }
        """;

    private string? previousConfigDirectory;
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareTestDirectory()
    {
        // Both directories are static state of the whole application, which is why this fixture
        // does not run alongside others:
        this.previousConfigDirectory = SettingsManager.ConfigDirectory;
        this.previousDataDirectory = SettingsManager.DataDirectory;

        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-settings-v7-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);

        SettingsManager.ConfigDirectory = this.testDirectory;
        SettingsManager.DataDirectory = this.testDirectory;
    }

    [TearDown]
    public void RemoveTestDirectory()
    {
        SettingsManager.ConfigDirectory = this.previousConfigDirectory;
        SettingsManager.DataDirectory = this.previousDataDirectory;

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public void TheSettingsOfTheAITasksKeepTheirValues()
    {
        var settings = Migrate(V6_SETTINGS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Version, Is.EqualTo(Version.V7));

            Assert.That(settings.TextContentCleaner.PreselectOptions, Is.True);
            Assert.That(settings.TextContentCleaner.PreselectedProvider, Is.EqualTo("cleaner"));
            Assert.That(settings.TextContentCleaner.MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));

            Assert.That(settings.DataSourceSelection.PreselectOptions, Is.True);
            Assert.That(settings.DataSourceSelection.PreselectedProvider, Is.EqualTo("selection"));
            Assert.That(settings.DataSourceSelection.MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.MEDIUM));

            Assert.That(settings.RetrievalContextValidation.EnableRetrievalContextValidation, Is.True);
            Assert.That(settings.RetrievalContextValidation.PreselectOptions, Is.True);
            Assert.That(settings.RetrievalContextValidation.PreselectedProvider, Is.EqualTo("validation"));
            Assert.That(settings.RetrievalContextValidation.MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.LOW));
            Assert.That(settings.RetrievalContextValidation.NumParallelValidations, Is.EqualTo(7));

            Assert.That(settings.AssistantPluginAudit.RequireAuditBeforeActivation, Is.False);
            Assert.That(settings.AssistantPluginAudit.PreselectedProvider, Is.EqualTo("audit"));
            Assert.That(settings.AssistantPluginAudit.MinimumProviderConfidence, Is.EqualTo(ConfidenceLevel.MODERATE));
        }
    }

    [Test]
    public void TheAssistantsKeepTheirChoiceOfTheContentCleaner()
    {
        var settings = Migrate(V6_SETTINGS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Translation.PreselectContentCleaner, Is.True);
            Assert.That(settings.Translation.PreselectedProvider, Is.EqualTo("translation"));
            Assert.That(settings.TextSummarizer.PreselectContentCleaner, Is.True);
            Assert.That(settings.TextSummarizer.PreselectedProvider, Is.EqualTo("summarizer"));
            Assert.That(settings.SWOTAnalysis.PreselectContentCleaner, Is.True);
            Assert.That(settings.SWOTAnalysis.PreselectedProvider, Is.EqualTo("swot"));
            Assert.That(settings.LegalCheck.PreselectContentCleaner, Is.True);
            Assert.That(settings.LegalCheck.PreselectedProvider, Is.EqualTo("legal"));
        }
    }

    /// <summary>
    /// Settings of version V5 pass every step up to V7, and the classes they share with V6 keep their former names on the way.
    /// </summary>
    [Test]
    public void SettingsOfVersionV5ArriveWithTheNewNames()
    {
        const string V5_SETTINGS = """
            {
              "Version": "V5",
              "Translation": { "PreselectContentCleanerAgent": true },
              "TextContentCleaner": { "PreselectAgentOptions": true, "PreselectedAgentProvider": "cleaner" },
              "AgentDataSourceSelection": { "PreselectAgentOptions": true, "PreselectedAgentProvider": "selection" },
              "AgentRetrievalContextValidation": { "EnableRetrievalContextValidation": true, "NumParallelValidations": 4 },
              "AssistantPluginAudit": { "PreselectedAgentProvider": "audit" }
            }
            """;

        var settings = SettingsMigrations.Migrate(NullLogger<SettingsManager>.Instance, Version.V5, V5_SETTINGS, SettingsManager.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Version, Is.EqualTo(Version.V7));
            Assert.That(settings.Translation.PreselectContentCleaner, Is.True);
            Assert.That(settings.TextContentCleaner.PreselectOptions, Is.True);
            Assert.That(settings.TextContentCleaner.PreselectedProvider, Is.EqualTo("cleaner"));
            Assert.That(settings.DataSourceSelection.PreselectOptions, Is.True);
            Assert.That(settings.DataSourceSelection.PreselectedProvider, Is.EqualTo("selection"));
            Assert.That(settings.RetrievalContextValidation.EnableRetrievalContextValidation, Is.True);
            Assert.That(settings.RetrievalContextValidation.NumParallelValidations, Is.EqualTo(4));
            Assert.That(settings.AssistantPluginAudit.PreselectedProvider, Is.EqualTo("audit"));
        }
    }

    [Test]
    public void TheManagedSettingsFollowTheNewNames()
    {
        var settings = Migrate(V6_SETTINGS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.ManagedLockedConfigurations, Is.EquivalentTo(new Dictionary<string, Guid>
            {
                ["DataAssistantPluginAudit.PreselectedProvider"] = CONFIG_PLUGIN_ID,
                ["DataApp.UpdateInterval"] = CONFIG_PLUGIN_ID,
            }), "A lock moves to the new name, and a setting which kept its name keeps its lock.");

            Assert.That(settings.ManagedEditableDefaults.Keys, Is.EquivalentTo(new[] { "DataDataSourceSelection.PreselectedProvider" }));
            Assert.That(settings.ManagedEditableDefaults["DataDataSourceSelection.PreselectedProvider"].ConfigPluginId, Is.EqualTo(CONFIG_PLUGIN_ID));
            Assert.That(settings.ManagedEditableDefaults["DataDataSourceSelection.PreselectedProvider"].LastAppliedValue, Is.EqualTo("selection"));

            Assert.That(settings.ManagedUserValueSnapshots, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["DataRetrievalContextValidation.NumParallelValidations"] = "5",
            }));
        }
    }

    [Test]
    public void EveryRenamedManagedSettingIsRegistered()
    {
        _ = new Data();
        var registeredSettingNames = ManagedConfiguration.RegisteredSettings.Select(setting => setting.SettingName).ToHashSet(StringComparer.Ordinal);

        using (Assert.EnterMultipleScope())
        {
            foreach (var (formerName, newName) in SettingsMigrations.MANAGED_SETTING_NAMES_V6_TO_V7)
            {
                Assert.That(registeredSettingNames, Does.Contain(newName), $"The clean-up of the managed settings would drop '{newName}', the new name of '{formerName}'.");
                Assert.That(registeredSettingNames, Does.Not.Contain(formerName), $"The former name '{formerName}' is still registered, so it needs no new name.");
            }
        }
    }

    [Test]
    public async Task LoadingSettingsOfVersionV6StoresThemWithTheNewNames()
    {
        var settingsPath = Path.Combine(this.testDirectory, SETTINGS_FILENAME);
        await File.WriteAllTextAsync(settingsPath, V6_SETTINGS);

        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        await settingsManager.LoadSettings();

        var stored = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath))!.AsObject();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedProvider, Is.EqualTo("audit"));
            Assert.That(stored["Version"]!.GetValue<string>(), Is.EqualTo(nameof(Version.V7)));
            Assert.That(stored.ContainsKey("DataSourceSelection"), Is.True);
            Assert.That(stored.ContainsKey("AgentDataSourceSelection"), Is.False);
            Assert.That(stored["ManagedLockedConfigurations"]!.AsObject().ContainsKey("DataAssistantPluginAudit.PreselectedProvider"), Is.True);
        }
    }

    private static Data Migrate(string settingsJson) => SettingsMigrations.Migrate(NullLogger<SettingsManager>.Instance, Version.V6, settingsJson, SettingsManager.JSON_OPTIONS);
}