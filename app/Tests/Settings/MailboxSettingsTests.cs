using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how the mailboxes are kept in the settings file.
/// </summary>
/// <remarks>
/// The mailboxes have a list of their own next to the data sources, so that the settings version
/// can stay where it is: an older app skips the list instead of failing over a data source type it
/// does not know. That only holds while a file with mailboxes still loads as the current version,
/// without a migration and without blocking the writes.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class MailboxSettingsTests
{
    private const string SETTINGS_FILENAME = "settings.json";

    /// <summary>
    /// A mailbox which differs from the defaults wherever it can, so that a value lost on the way
    /// cannot hide behind a default.
    /// </summary>
    private static readonly DataSourceMailbox MAILBOX = new()
    {
        Num = 3,
        Id = "2f6c1d0e-8b4a-4f3e-9c7d-1a2b3c4d5e6f",
        Name = "Work",
        EmbeddingId = "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 32,
        ConfidenceLevel = ConfidenceLevel.MEDIUM,
        Host = "imap.example.org",
        Port = 143,
        TransportSecurity = MailboxTransportSecurity.STARTTLS,
        AuthMethod = MailboxAuthMethod.PASSWORD,
        Username = "someone@example.org",
        RootFolder = "INBOX/Projects",
        MaxAge = MailboxMaxAge.LAST_24_MONTHS,
        IndexAttachments = false,
        MaxAttachmentSizeMegabytes = 25,
        OutboundDataRestriction = OutboundDataRestriction.ONLY_LINKS_FROM_CHAT,
        MaxMatches = 20,
    };

    private string? previousConfigDirectory;
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareTestDirectory()
    {
        //
        // Both directories are static state of the whole application, which is why this fixture
        // does not run alongside others. They are put back in the teardown.
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
    public void AMailboxComesBackAsItWasStored()
    {
        var json = JsonSerializer.Serialize(new Data { Mailboxes = [MAILBOX] }, SettingsManager.JSON_OPTIONS);
        var data = JsonSerializer.Deserialize<Data>(json, SettingsManager.JSON_OPTIONS);

        Assert.That(data?.Mailboxes, Is.EqualTo(new[] { MAILBOX }));
    }

    [Test]
    public async Task ASettingsFileWithMailboxesLoadsAsTheCurrentVersion()
    {
        var stored = new Data { Mailboxes = [MAILBOX] };
        await File.WriteAllTextAsync(Path.Combine(this.testDirectory, SETTINGS_FILENAME), JsonSerializer.Serialize(stored, SettingsManager.JSON_OPTIONS));

        var settingsManager = CreateSettingsManager();
        var loaded = await settingsManager.TryReadSettingsSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(settingsManager.SettingsWriteBlockReason, Is.EqualTo(SettingsWriteBlockReason.NONE), "The mailboxes kept the settings from being written.");
            Assert.That(loaded?.Version, Is.EqualTo(stored.Version), "The settings were loaded as another version.");
            Assert.That(loaded?.Mailboxes, Is.EqualTo(new[] { MAILBOX }), "The mailboxes were lost while loading.");
        });
    }

    [Test]
    public async Task ASettingsFileFromBeforeTheMailboxesLoadsWithoutAny()
    {
        var currentVersion = new Data().Version;
        await File.WriteAllTextAsync(Path.Combine(this.testDirectory, SETTINGS_FILENAME), $$"""{"Version": "{{currentVersion}}", "DataSources": []}""");

        var settingsManager = CreateSettingsManager();
        var loaded = await settingsManager.TryReadSettingsSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(settingsManager.SettingsWriteBlockReason, Is.EqualTo(SettingsWriteBlockReason.NONE), "The settings without mailboxes were blocked from being written.");
            Assert.That(loaded?.Mailboxes, Is.Empty, "Mailboxes appeared out of nowhere.");
        });
    }

    [Test]
    public void ValuesThisVersionCannotReadFallBackToTheSafeSide()
    {
        //
        // A newer version may know values this one does not, and the settings file arrives here
        // after a downgrade. Each of them falls back to the member with the underlying value 0,
        // and each enum chooses that member so that nothing gets less safe on the way: no connection,
        // no sign-in, the strictest restriction, and no provider at all.
        //
        const string JSON = """
                            {
                                "ConfidenceLevel": "EXTREMELY_HIGH",
                                "TransportSecurity": "QUANTUM_TLS",
                                "AuthMethod": "PASSKEY",
                                "MaxAge": "LAST_36_MONTHS",
                                "OutboundDataRestriction": "NOTHING_AT_ALL"
                            }
                            """;

        var mailbox = JsonSerializer.Deserialize<DataSourceMailbox>(JSON, SettingsManager.JSON_OPTIONS);
        Assert.Multiple(() =>
        {
            Assert.That(mailbox.ConfidenceLevel, Is.EqualTo(ConfidenceLevel.NONE));
            Assert.That(mailbox.TransportSecurity, Is.EqualTo(MailboxTransportSecurity.UNKNOWN));
            Assert.That(mailbox.AuthMethod, Is.EqualTo(MailboxAuthMethod.UNKNOWN));
            Assert.That(mailbox.MaxAge, Is.EqualTo(MailboxMaxAge.LAST_3_MONTHS));
            Assert.That(mailbox.OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
        });
    }

    /// <remarks>
    /// The rust service is handed in as null on purpose, as in SettingsStorageTests: reading the
    /// settings never asks it anything.
    /// </remarks>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);
}