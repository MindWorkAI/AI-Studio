using System.Text.Json;

using AIStudio.Tools.DataBackup;

using SettingsVersion = AIStudio.Settings.Version;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks what the first start after a restore, a crashed backup, or a crashed preparation removes,
/// and what it must keep.
/// </summary>
/// <remarks>
/// While the runtime has not finished a restore, its journal and the data it replaced are the only way
/// back. Removing them would lose the data of the user for good.
/// </remarks>
[TestFixture]
public sealed class DataRestoreFollowUpTests
{
    private string testDirectory = string.Empty;

    private string DataDirectory => Path.Combine(this.testDirectory, "data");

    private string ConfigDirectory => Path.Combine(this.testDirectory, "config");

    [SetUp]
    public void CreateFolders() => this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-restore-follow-up-{Guid.NewGuid():N}");

    [TearDown]
    public void RemoveFolders()
    {
        if (Directory.Exists(this.testDirectory))
            Directory.Delete(this.testDirectory, true);
    }

    [Test]
    public void WhatABackupOrAPreparationLeftHalfwayIsRemoved()
    {
        WriteFile(Path.Combine(this.DataDirectory, ".backup-staging", "0123456789abcdef", "store"), "copy");
        WriteFile(Path.Combine(this.DataDirectory, ".restore-pending", "workspaces", "chat", "thread.json"), "{}");
        WriteFile(Path.Combine(this.ConfigDirectory, ".restore-pending", "settings.json"), "{}");

        DataRestoreFollowUp.RemoveLeftovers(this.DataDirectory, this.ConfigDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(this.DataDirectory, ".backup-staging")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.DataDirectory, ".restore-pending")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.ConfigDirectory, ".restore-pending")), Is.False);
        });
    }

    [Test]
    public void ARestoreWhichWaitsForTheRestartIsKept()
    {
        WriteFile(Path.Combine(this.DataDirectory, ".restore-pending", "workspaces", "chat", "thread.json"), "{}");
        WriteFile(Path.Combine(this.DataDirectory, ".restore-pending", DataRestoreFormat.READY_FILE_NAME), "now");

        DataRestoreFollowUp.RemoveLeftovers(this.DataDirectory, this.ConfigDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(DataRestoreFollowUp.IsRestorePending(this.DataDirectory), Is.True);
            Assert.That(DataRestoreFollowUp.IsRestoreUnfinished(this.DataDirectory), Is.False);
            Assert.That(File.Exists(Path.Combine(this.DataDirectory, ".restore-pending", "workspaces", "chat", "thread.json")), Is.True);
        });
    }

    [Test]
    public void ARestoreWhichTheRuntimeHasNotFinishedKeepsEverything()
    {
        WriteFile(Path.Combine(this.DataDirectory, ".restore-pending", DataRestoreFormat.READY_FILE_NAME), "now");
        WriteFile(Path.Combine(this.DataDirectory, ".restore-pending", DataRestoreFormat.JOURNAL_FILE_NAME), """{ "moves": [], "started": 0 }""");
        WriteFile(Path.Combine(this.DataDirectory, ".restore-previous", "workspaces", "chat", "thread.json"), "{}");
        var target = new DataRestoreTarget(this.DataDirectory, this.ConfigDirectory, "26.10.1", SettingsVersion.V6, []);

        DataRestoreFollowUp.RemoveLeftovers(this.DataDirectory, this.ConfigDirectory);
        var removedReplacedData = DataRestoreFollowUp.RemoveReplacedData(this.DataDirectory, this.ConfigDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(DataRestoreFollowUp.IsRestoreUnfinished(this.DataDirectory), Is.True);
            Assert.That(DataRestoreFollowUp.IsRestorePending(this.DataDirectory), Is.False, "The runtime moves it back rather than applying it.");
            Assert.That(removedReplacedData, Is.False);
            Assert.That(File.Exists(Path.Combine(this.DataDirectory, ".restore-previous", "workspaces", "chat", "thread.json")), Is.True, "These are the data of the user.");
            Assert.That(() => DataRestorePreparer.Discard(target), Throws.InvalidOperationException);
            Assert.That(File.Exists(Path.Combine(this.DataDirectory, ".restore-pending", DataRestoreFormat.JOURNAL_FILE_NAME)), Is.True);
        });
    }

    [Test]
    public void TheReplacedDataAreRemovedOnceNoRestoreWaits()
    {
        WriteFile(Path.Combine(this.DataDirectory, ".restore-previous", "workspaces", "chat", "thread.json"), "{}");
        WriteFile(Path.Combine(this.ConfigDirectory, ".restore-previous", "settings.json"), "{}");

        var removedReplacedData = DataRestoreFollowUp.RemoveReplacedData(this.DataDirectory, this.ConfigDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(removedReplacedData, Is.True);
            Assert.That(Directory.Exists(Path.Combine(this.DataDirectory, ".restore-previous")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.ConfigDirectory, ".restore-previous")), Is.False);
        });
    }

    [Test]
    public async Task TheReportOfTheRuntimeIsRead()
    {
        WriteFile(Path.Combine(this.DataDirectory, DataRestoreFormat.RESULT_FILE_NAME), """
            {
              "outcome": "ROLLED_BACK",
              "detail": "Could not move '.restore-pending/plugins' to 'plugins': access denied",
              "prepared_at_utc": "2026-10-15T08:00:00.0000000+00:00",
              "backup_created_at_utc": "2026-10-01T17:30:00.0000000+00:00",
              "backup_app_version": "26.10.1",
              "finished_at_utc": "2026-10-15T08:01:00.123456+00:00"
            }
            """);

        var report = await DataRestoreFollowUp.ReadReportAsync(this.DataDirectory, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(report, Is.Not.Null);
            Assert.That(report!.Outcome, Is.EqualTo(DataRestoreOutcome.ROLLED_BACK));
            Assert.That(report.BackupAppVersion, Is.EqualTo("26.10.1"));
            Assert.That(report.Detail, Does.Contain("access denied"));
        });
    }

    [Test]
    public async Task WithoutARestoreThereIsNoReport()
    {
        Directory.CreateDirectory(this.DataDirectory);

        var report = await DataRestoreFollowUp.ReadReportAsync(this.DataDirectory, CancellationToken.None);

        Assert.That(report, Is.Null);
    }

    [Test]
    public void AReportWithAnUnknownOutcomeCannotBeRead()
    {
        WriteFile(Path.Combine(this.DataDirectory, DataRestoreFormat.RESULT_FILE_NAME), """{ "outcome": "SOMETHING_NEW", "detail": "", "prepared_at_utc": "", "backup_created_at_utc": "", "backup_app_version": "", "finished_at_utc": "" }""");

        Assert.That(async () => await DataRestoreFollowUp.ReadReportAsync(this.DataDirectory, CancellationToken.None), Throws.InstanceOf<JsonException>());
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}