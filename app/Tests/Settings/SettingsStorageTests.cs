using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

using Version = AIStudio.Settings.Version;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what settings operations do to each other when they overlap.
/// </summary>
/// <remarks>
/// The settings are written from everywhere: a timer firing on its own thread, a dialog the user
/// just closed, a configuration plugin which arrived over the network. Nothing keeps two of those
/// from meeting, and what they must never leave behind is a settings file nobody can read -- it is
/// the file the app starts from the next morning. These tests arrange the meeting on purpose and
/// look at what is on the disk afterward.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class SettingsStorageTests
{
    /// <summary>
    /// How many operations are set against each other.
    /// </summary>
    /// <remarks>
    /// High enough that the operations genuinely overlap on any machine, low enough that the test
    /// stays a test. A race which needs more than this to show up would not be one the app meets.
    /// </remarks>
    private const int CONCURRENT_OPERATIONS = 50;

    private const string SETTINGS_FILENAME = "settings.json";

    private const string BACKUP_FILENAME = "settings.v6.json";

    private string? previousConfigDirectory;
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareTestDirectory()
    {
        //
        // Both directories are static state of the whole application, which is why this fixture
        // does not run alongside others. They are put back in the teardown so that a later test
        // does not inherit a directory which is gone by then.
        //
        this.previousConfigDirectory = SettingsManager.ConfigDirectory;
        this.previousDataDirectory = SettingsManager.DataDirectory;

        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-settings-{Guid.NewGuid():N}");
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
    public async Task OverlappingStoresLeaveBothFilesReadable()
    {
        var settingsManager = CreateSettingsManager();
        await Task.WhenAll(Enumerable.Range(0, CONCURRENT_OPERATIONS).Select(_ => settingsManager.StoreSettings()));

        var settingsPath = Path.Combine(this.testDirectory, SETTINGS_FILENAME);
        var backupPath = Path.Combine(this.testDirectory, BACKUP_FILENAME);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(settingsPath), Is.True, "The settings file was never written.");
            Assert.That(File.Exists(backupPath), Is.True, "The settings backup file was never written.");
            Assert.That(ReadSettingsFile(settingsPath)?.Version, Is.EqualTo(Version.V6), "The settings file could not be read back.");
            Assert.That(ReadSettingsFile(backupPath)?.Version, Is.EqualTo(Version.V6), "The settings backup file could not be read back.");
        });
    }

    [Test]
    public async Task OverlappingStoresLeaveNoTemporaryFilesBehind()
    {
        var settingsManager = CreateSettingsManager();
        await Task.WhenAll(Enumerable.Range(0, CONCURRENT_OPERATIONS).Select(_ => settingsManager.StoreSettings()));

        //
        // Every store writes its settings next to the previous ones and renames afterwards. The
        // temporary file carries a name of its own, so two stores cannot collide over it -- but a
        // store which gave up halfway would leave one lying around, and the next start would find
        // a configuration directory filling up with them.
        //
        var leftovers = Directory.GetFiles(this.testDirectory, "*.tmp-*").Select(Path.GetFileName).ToList();
        Assert.That(leftovers, Is.Empty, $"Temporary settings files were left behind: {string.Join(", ", leftovers)}.");
    }

    [Test]
    public async Task AStoreCannotSlipThroughWhileAReadReconsidersTheWriteBlock()
    {
        var settingsPath = Path.Combine(this.testDirectory, SETTINGS_FILENAME);

        //
        // Settings written by a newer app than this one. Reading them blocks every write, so that
        // this app cannot replace settings it does not understand with the little it does. What
        // makes this the interesting case is how a read arrives at that verdict: it clears the
        // block first and only re-establishes it once it has seen the file. A store meeting that
        // moment would find nothing standing in its way and overwrite the very file the block
        // exists for -- which is why a read holds the same lock a store does.
        //
        await File.WriteAllTextAsync(settingsPath, """{"Version": "V99"}""");

        var settingsManager = CreateSettingsManager();
        await settingsManager.TryReadSettingsSnapshot();
        Assert.That(settingsManager.SettingsWriteBlockReason, Is.EqualTo(SettingsWriteBlockReason.VERSION_NEWER_THAN_APP), "The newer settings file did not block writes in the first place.");

        var operations = new List<Task>();
        for (var i = 0; i < CONCURRENT_OPERATIONS; i++)
        {
            operations.Add(settingsManager.StoreSettings());
            operations.Add(settingsManager.TryReadSettingsSnapshot());
        }

        await Task.WhenAll(operations);

        using var settingsDocument = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
        Assert.Multiple(() =>
        {
            Assert.That(settingsDocument.RootElement.GetProperty("Version").GetString(), Is.EqualTo("V99"), "A store overwrote the newer settings file while a read was reconsidering the write block.");
            Assert.That(settingsManager.SettingsWriteBlockReason, Is.EqualTo(SettingsWriteBlockReason.VERSION_NEWER_THAN_APP), "The write block did not survive the reads which re-established it.");
        });
    }

    /// <summary>
    /// Builds a settings manager the way these tests need it.
    /// </summary>
    /// <remarks>
    /// The rust service is handed in as null on purpose: neither storing nor reading settings ever
    /// asks it anything. Only the active language is read through it, and that is not what is being
    /// checked here. Should a future store reach for it, the test says so by failing loudly rather
    /// than by quietly testing a different thing.
    /// </remarks>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static Data? ReadSettingsFile(string settingsPath) => JsonSerializer.Deserialize<Data>(File.ReadAllText(settingsPath), SettingsManager.JSON_OPTIONS);
}