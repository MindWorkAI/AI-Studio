using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.DataBackup;

using Microsoft.Extensions.Logging.Abstractions;

using Version = AIStudio.Settings.Version;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Reads and writes stored files of AI Studio, and checks that nothing gets lost and nothing gets written
/// otherwise than with the contracts of reflection.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written with the contracts of reflection, before AI Studio got
/// contracts of its own, and every property holds a value. They are made up: no real chat or setting of
/// anybody is among them. A property added later is missing in them, which the tests allow.
/// </remarks>
[TestFixture]
public sealed class JsonGoldenFileTests
{
    private static IEnumerable<JsonGoldenFile> GoldenFiles()
    {
        TestHost.PrepareStaticState();
        yield return new("settings.json", typeof(Data), SettingsManager.JSON_OPTIONS);
        yield return new("settings-v1.json", typeof(DataV1V3), SettingsManager.JSON_OPTIONS);
        yield return new("settings-v4.json", typeof(DataV4), SettingsManager.JSON_OPTIONS);
        yield return new("settings-v5.json", typeof(DataV5), SettingsManager.JSON_OPTIONS);
        yield return new("chat.json", typeof(ChatThread), WorkspaceBehaviour.JSON_OPTIONS);
        yield return new("swot-analysis.json", typeof(SwotAnalysisResult), SwotAnalysisContentPresentation.JSON_OPTIONS);
        yield return new("data-backup-manifest.json", typeof(DataBackupManifest), DataBackupFormat.JSON_OPTIONS);
        yield return new("data-restore-plan.json", typeof(DataRestorePlan), DataBackupFormat.JSON_OPTIONS);
        yield return new("data-restore-report.json", typeof(DataRestoreReport), DataBackupFormat.JSON_OPTIONS);
        yield return new("provenance-surrounding-content.json", typeof(List<string>), SqliteProvenanceStoreClientImplementation.JSON_OPTIONS);
    }

    [TestCaseSource(nameof(GoldenFiles))]
    public void AStoredFileIsWrittenAgainAsWithReflection(JsonGoldenFile goldenFile)
    {
        var json = ReadFixture(goldenFile.FileName);
        var baseline = JsonReflectionBaseline.Of(goldenFile.Options);

        var writtenAgain = JsonSerializer.Serialize(JsonSerializer.Deserialize(json, goldenFile.Type, goldenFile.Options), goldenFile.Type, goldenFile.Options);
        var writtenAgainByReflection = JsonSerializer.Serialize(JsonSerializer.Deserialize(json, goldenFile.Type, baseline), goldenFile.Type, baseline);

        Assert.That(writtenAgain, Is.EqualTo(writtenAgainByReflection));
    }

    [TestCaseSource(nameof(GoldenFiles))]
    public void AStoredFileKeepsAllItsValues(JsonGoldenFile goldenFile)
    {
        var json = ReadFixture(goldenFile.FileName);
        var writtenAgain = JsonSerializer.Serialize(JsonSerializer.Deserialize(json, goldenFile.Type, goldenFile.Options), goldenFile.Type, goldenFile.Options);

        Assert.That(JsonContainment.FindMissing(JsonNode.Parse(json), JsonNode.Parse(writtenAgain)), Is.Empty);
    }

    [TestCase("settings-v1.json", Version.V1)]
    [TestCase("settings-v4.json", Version.V4)]
    [TestCase("settings-v5.json", Version.V5)]
    public void OldSettingsMigrateAsWithReflection(string fileName, Version version)
    {
        var json = ReadFixture(fileName);
        var baseline = JsonReflectionBaseline.Of(SettingsManager.JSON_OPTIONS);

        var migrated = SettingsMigrations.Migrate(NullLogger<SettingsManager>.Instance, version, json, SettingsManager.JSON_OPTIONS);
        var migratedByReflection = SettingsMigrations.Migrate(NullLogger<SettingsManager>.Instance, version, json, baseline);

        Assert.That(JsonSerializer.Serialize(migrated, SettingsManager.JSON_OPTIONS), Is.EqualTo(JsonSerializer.Serialize(migratedByReflection, baseline)));
    }

    /// <summary>
    /// Reads a file from the fixtures next to this test.
    /// </summary>
    /// <remarks>
    /// Read from the source tree, the way the other fixtures are, so the file needs no entry in the project file.
    /// </remarks>
    private static string ReadFixture(string fileName, [CallerFilePath] string sourceFilePath = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Fixtures", fileName));
}