using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks that the data source defaults of new chats survive the settings file.
/// </summary>
/// <remarks>
/// The settings file holds each of these defaults twice: as a field of its own, and inside the
/// legacy nested object, see documentation/compatibility-shims/2026-07-chat-data-source-options.md.
/// The nested object is read after the fields and therefore wins. A default which the nested object
/// forgets to carry would go back to the app default with every start, and nobody would notice
/// before wondering why the choice never sticks.
/// </remarks>
[TestFixture]
public sealed class ChatDataSourceDefaultsTests
{
    [Test]
    public void TheRetrievalModeSurvivesTheSettingsFile()
    {
        var written = new DataChat { PreselectedDataSourcesRetrievalMode = DataSourceRetrievalMode.EVERY_MESSAGE };

        var json = JsonSerializer.Serialize(written, SettingsManager.JSON_OPTIONS);
        var read = JsonSerializer.Deserialize<DataChat>(json, SettingsManager.JSON_OPTIONS)!;

        Assert.That(read.PreselectedDataSourcesRetrievalMode, Is.EqualTo(DataSourceRetrievalMode.EVERY_MESSAGE), "Semantic search is the default, so only the other choice shows that the settings file keeps it.");
    }

    [Test]
    public void ASettingsFileFromBeforeTheChoiceGetsSemanticSearch()
    {
        const string LEGACY_JSON = """
                                   {
                                     "PreselectedDataSourceOptions": {
                                       "DisableDataSources": false,
                                       "AutomaticDataSourceSelection": true,
                                       "AutomaticValidation": true,
                                       "PreselectedDataSourceIds": []
                                     }
                                   }
                                   """;

        var read = JsonSerializer.Deserialize<DataChat>(LEGACY_JSON, SettingsManager.JSON_OPTIONS)!;

        Assert.Multiple(() =>
        {
            Assert.That(read.PreselectedDataSourcesDisabled, Is.False, "The legacy nested object has to reach the individual fields, or this test checks nothing.");
            Assert.That(read.PreselectedDataSourcesRetrievalMode, Is.EqualTo(DataSourceRetrievalMode.SEMANTIC_SEARCH));
        });
    }
}