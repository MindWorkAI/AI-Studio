using System.IO.Compression;
using System.Text.Json;

using AIStudio.Tools.DataBackup;

using SettingsVersion = AIStudio.Settings.Version;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks how a restore is prepared for the next start.
/// </summary>
/// <remarks>
/// The runtime replaces the data at the next start with whatever the preparation left behind, as soon
/// as it finds it marked as ready. A damaged backup or one this version cannot read must therefore
/// never get that far, and the plan has to name exactly what to replace and what to keep.
/// </remarks>
[TestFixture]
public sealed class DataRestorePreparerTests
{
    private static readonly Guid WORKSPACE_ID = Guid.Parse("3f2b7c1e-9a4d-4e6b-8c2f-1d5e7a9b0c3d");

    private static readonly Guid CHAT_ID = Guid.Parse("8a1c4e7f-2b5d-4f9a-b3c6-0e2d4f6a8b1c");

    private static readonly DateTime CHAT_LAST_CHANGED = new(2026, 9, 1, 8, 30, 0, DateTimeKind.Local);

    private string testDirectory = string.Empty;

    private string ArchivePath => Path.Combine(this.testDirectory, "backup.mwbackup");

    private DataRestoreTarget Target => new(Path.Combine(this.testDirectory, "target-data"), Path.Combine(this.testDirectory, "target-config"), "26.10.1", SettingsVersion.V6, ["plugins/company-assistant/"]);

    [SetUp]
    public void CreateFolders() => this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-restore-preparer-{Guid.NewGuid():N}");

    [TearDown]
    public void RemoveFolders()
    {
        if (Directory.Exists(this.testDirectory))
            Directory.Delete(this.testDirectory, true);
    }

    [Test]
    public async Task ARestoreIsUnpackedNextToTheFoldersAndMarkedReadyLast()
    {
        await this.WriteBackupAsync("26.10.1", "V6");

        var result = await DataRestorePreparer.PrepareAsync(this.ArchivePath, this.Target, null, CancellationToken.None);
        var dataPending = Path.Combine(this.Target.DataDirectory, ".restore-pending");
        var threadPath = Path.Combine(dataPending, "workspaces", WORKSPACE_ID.ToString(), CHAT_ID.ToString(), "thread.json");
        var plan = JsonSerializer.Deserialize<DataRestorePlan>(await File.ReadAllTextAsync(Path.Combine(dataPending, DataRestoreFormat.PLAN_FILE_NAME)), DataBackupFormat.JSON_OPTIONS)!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Issue);
            Assert.That(DataRestorePreparer.IsPrepared(this.Target), Is.True);
            Assert.That(File.ReadAllText(threadPath), Is.EqualTo("{}"));
            Assert.That(File.GetLastWriteTime(threadPath), Is.EqualTo(CHAT_LAST_CHANGED).Within(TimeSpan.FromSeconds(2)), "The workspaces sort their chats by it.");
            Assert.That(File.ReadAllText(Path.Combine(dataPending, "plugins", "my-assistant", "plugin.lua")), Is.EqualTo("-- mine"));
            Assert.That(File.ReadAllText(Path.Combine(this.Target.ConfigDirectory, ".restore-pending", "settings.json")), Does.Contain("V6"), "The settings go next to the configuration folder.");

            Assert.That(plan.Elements.Select(element => $"{element.Location}:{element.RelativePath}"), Is.EquivalentTo((string[])["CONFIG_DIRECTORY:settings.json", "DATA_DIRECTORY:workspaces/", "DATA_DIRECTORY:plugins/"]), "An element the backup holds nothing for, such as the tokenizers here, stays as it is.");
            Assert.That(plan.Elements.Single(element => element.RelativePath == "plugins/").Keep, Is.SupersetOf((string[])["plugins/.internal/", "plugins/.config/", "plugins/.config-tests/", "plugins/company-assistant/"]), "AI Studio and the organization bring their plugins themselves.");
        });
    }

    [Test]
    public async Task ADamagedBackupLeavesNothingBehind()
    {
        await this.WriteBackupAsync("26.10.1", "V6", damageThread: true);

        var result = await DataRestorePreparer.PrepareAsync(this.ArchivePath, this.Target, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Issue, Does.Contain("thread.json"));
            Assert.That(Directory.Exists(Path.Combine(this.Target.DataDirectory, ".restore-pending")), Is.False, "The next start must find nothing to apply.");
            Assert.That(Directory.Exists(Path.Combine(this.Target.ConfigDirectory, ".restore-pending")), Is.False);
        });
    }

    [TestCase("27.1.0", "V6")]
    [TestCase("26.10.1", "V7")]
    public async Task ABackupOfANewerVersionIsRefused(string appVersion, string settingsVersion)
    {
        await this.WriteBackupAsync(appVersion, settingsVersion);

        var result = await DataRestorePreparer.PrepareAsync(this.ArchivePath, this.Target, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False, "Its settings or its index may have a form this version cannot read.");
            Assert.That(DataRestorePreparer.IsPrepared(this.Target), Is.False);
        });
    }

    [Test]
    public async Task ADiscardedRestoreLeavesNothingBehind()
    {
        await this.WriteBackupAsync("26.10.1", "V6");
        await DataRestorePreparer.PrepareAsync(this.ArchivePath, this.Target, null, CancellationToken.None);

        DataRestorePreparer.Discard(this.Target);

        Assert.Multiple(() =>
        {
            Assert.That(DataRestorePreparer.IsPrepared(this.Target), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.Target.ConfigDirectory, ".restore-pending")), Is.False);
        });
    }

    /// <summary>
    /// Writes a backup of a data folder with a chat and a plugin of the user.
    /// </summary>
    /// <param name="appVersion">The version of AI Studio the manifest names.</param>
    /// <param name="settingsVersion">The version of the settings the manifest names.</param>
    /// <param name="damageThread">Whether to change the chat thread after the manifest noted its hash.</param>
    private async Task WriteBackupAsync(string appVersion, string settingsVersion, bool damageThread = false)
    {
        var sourceDataDirectory = Path.Combine(this.testDirectory, "source-data");
        var threadPath = Path.Combine(sourceDataDirectory, "workspaces", WORKSPACE_ID.ToString(), CHAT_ID.ToString(), "thread.json");
        WriteFile(Path.Combine(sourceDataDirectory, "workspaces", WORKSPACE_ID.ToString(), "name"), "Projects");
        WriteFile(threadPath, "{}");
        File.SetLastWriteTime(threadPath, CHAT_LAST_CHANGED);
        WriteFile(Path.Combine(sourceDataDirectory, "plugins", "my-assistant", "plugin.lua"), "-- mine");

        var sources = new DataBackupSources(sourceDataDirectory, Path.Combine(this.testDirectory, "source-config"), """{ "Version": "V6" }""", string.Empty, []);
        var manifest = new DataBackupManifest(DataBackupFormat.FORMAT_VERSION, appVersion, settingsVersion, DateTimeOffset.UtcNow, sourceDataDirectory, string.Empty, false, []);
        await using (var archiveStream = File.Create(this.ArchivePath))
            await DataBackupWriter.WriteAsync(archiveStream, sources, manifest, null, CancellationToken.None);

        if (!damageThread)
            return;

        // Rewrite the backup with another chat thread, but the manifest of the original one:
        var damagedArchivePath = $"{this.ArchivePath}.damaged";
        await using (var originalStream = File.OpenRead(this.ArchivePath))
        using (var original = new ZipArchive(originalStream, ZipArchiveMode.Read))
        await using (var damagedStream = File.Create(damagedArchivePath))
        using (var damaged = new ZipArchive(damagedStream, ZipArchiveMode.Create))
        {
            foreach (var entry in original.Entries)
            {
                await using var target = damaged.CreateEntry(entry.FullName).Open();
                if (entry.FullName.EndsWith("thread.json", StringComparison.Ordinal))
                {
                    await target.WriteAsync("{ }"u8.ToArray());
                    continue;
                }

                await using var source = entry.Open();
                await source.CopyToAsync(target);
            }
        }

        File.Move(damagedArchivePath, this.ArchivePath, overwrite: true);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}