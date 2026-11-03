using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks in which format an installation exports encrypted secrets, depending on whether it
/// existed before the format became a choice.
/// </summary>
/// <remarks>
/// Large organizations package updates themselves, so their administrators run a new version weeks
/// before everybody else. Exports in the authenticated format v2 would be unreadable for the rest
/// of such an organization meanwhile. An installation which existed before v26.10.1 therefore keeps
/// v1, and only a new one starts with v2. Keeping v1 is a compatibility shim, see
/// documentation/compatibility-shims/2026-10-enterprise-encryption-export-format.md.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class EnterpriseEncryptionExportFormatTests
{
    private const string SETTINGS_FILENAME = "settings.json";

    private string? previousConfigDirectory;
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    private string SettingsPath => Path.Combine(this.testDirectory, SETTINGS_FILENAME);

    [SetUp]
    public void PrepareTestDirectory()
    {
        // Both directories are static state of the whole application, which is why this fixture
        // does not run alongside others:
        this.previousConfigDirectory = SettingsManager.ConfigDirectory;
        this.previousDataDirectory = SettingsManager.DataDirectory;

        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-encryption-format-{Guid.NewGuid():N}");
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
    public async Task ANewInstallationStartsWithTheAuthenticatedFormat()
    {
        var settingsManager = CreateSettingsManager();
        await settingsManager.LoadSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(this.SettingsPath), Is.False, "A new installation has no settings file yet.");
            Assert.That(settingsManager.ConfigurationData.App.EnterpriseEncryptionExportFormat, Is.EqualTo(EnterpriseEncryptionFormat.V2));
        }
    }

    [Test]
    public async Task SettingsFromBeforeTheChoiceKeepTheLegacyFormat()
    {
        var settings = SerializeToNode(new Data());
        settings["App"]!.AsObject().Remove(nameof(DataApp.EnterpriseEncryptionExportFormat));
        await File.WriteAllTextAsync(this.SettingsPath, settings.ToJsonString(SettingsManager.JSON_OPTIONS));

        var settingsManager = CreateSettingsManager();
        await settingsManager.LoadSettings();
        await settingsManager.StoreSettings();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.ConfigurationData.App.EnterpriseEncryptionExportFormat, Is.EqualTo(EnterpriseEncryptionFormat.V1));
            Assert.That(this.ReadStoredFormat(), Is.EqualTo(nameof(EnterpriseEncryptionFormat.V1)), "Once stored, the settings hold the format, and the shim has nothing left to do.");
        }
    }

    [Test]
    public async Task SettingsOfAnOlderVersionKeepTheLegacyFormat()
    {
        await File.WriteAllTextAsync(this.SettingsPath, """{"Version": "V5"}""");

        var settingsManager = CreateSettingsManager();
        await settingsManager.LoadSettings();

        Assert.That(settingsManager.ConfigurationData.App.EnterpriseEncryptionExportFormat, Is.EqualTo(EnterpriseEncryptionFormat.V1));
    }

    [TestCase(EnterpriseEncryptionFormat.V1)]
    [TestCase(EnterpriseEncryptionFormat.V2)]
    public async Task AChosenFormatStays(EnterpriseEncryptionFormat format)
    {
        var data = new Data();
        data.App.EnterpriseEncryptionExportFormat = format;
        await File.WriteAllTextAsync(this.SettingsPath, SerializeToNode(data).ToJsonString(SettingsManager.JSON_OPTIONS));

        var settingsManager = CreateSettingsManager();
        await settingsManager.LoadSettings();

        Assert.That(settingsManager.ConfigurationData.App.EnterpriseEncryptionExportFormat, Is.EqualTo(format));
    }

    /// <remarks>
    /// The rust service is handed in as null on purpose: neither storing nor reading settings ever
    /// asks it anything.
    /// </remarks>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static JsonObject SerializeToNode(Data data) => JsonSerializer.SerializeToNode(data, SettingsManager.JSON_OPTIONS)!.AsObject();

    private string? ReadStoredFormat()
    {
        using var settingsDocument = JsonDocument.Parse(File.ReadAllText(this.SettingsPath));
        return settingsDocument.RootElement.GetProperty("App").GetProperty(nameof(DataApp.EnterpriseEncryptionExportFormat)).GetString();
    }
}