using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIStudio.Tools.DataBackup;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks what a backup holds.
/// </summary>
/// <remarks>
/// A restore unpacks the backup and replaces what the list of the backup contents names. A file
/// missing from it is lost on the next computer, and a file of the organization or of AI Studio
/// itself which got in would compete with the one the organization or AI Studio brings. The indexes
/// come from their copies only: read while AI Studio writes to them, they would not fit together.
/// </remarks>
[TestFixture]
public sealed class DataBackupWriterTests
{
    private const string SETTINGS_JSON = """{ "Version": "V6" }""";

    private static readonly Guid WORKSPACE_ID = Guid.Parse("3f2b7c1e-9a4d-4e6b-8c2f-1d5e7a9b0c3d");

    private static readonly Guid CHAT_ID = Guid.Parse("8a1c4e7f-2b5d-4f9a-b3c6-0e2d4f6a8b1c");

    private string testDirectory = string.Empty;

    private string DataDirectory => Path.Combine(this.testDirectory, "data");

    private string ConfigDirectory => Path.Combine(this.testDirectory, "config");

    private string IndexCopiesDirectory => Path.Combine(this.testDirectory, "index-copies");

    [SetUp]
    public void CreateFolders()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-backup-writer-{Guid.NewGuid():N}");

        this.WriteDataFile($"workspaces/{WORKSPACE_ID}/name", "Projects");
        this.WriteDataFile($"workspaces/{WORKSPACE_ID}/{CHAT_ID}/thread.json", "{}");
        this.WriteDataFile($"workspaces/{WORKSPACE_ID}/{CHAT_ID}/attachments/transcripts/meeting-transcript-0001.md", "Hello and welcome.");
        this.WriteDataFile($"tempChats/{Guid.Empty}/thread.json", "{}");
        this.WriteDataFile("plugins/my-assistant/plugin.lua", "-- mine");
        this.WriteDataFile("plugins/my-assistant/.hidden.lua", "-- hidden, but mine as well");
        this.WriteDataFile("plugins/company-assistant/plugin.lua", "-- deployed by the organization");
        this.WriteDataFile("plugins/.internal/language/plugin.lua", "-- comes with AI Studio");
        this.WriteDataFile("plugins/.config/0b5f1e8a-3c2d-4e6f-9a1b-7c8d9e0f1a2b/plugin.lua", "-- configuration of the organization");
        this.WriteDataFile("plugins/.lock", string.Empty);
        this.WriteDataFile("tokenizers/openai-gpt-6/tokenizer.json", "{}");
        this.WriteDataFile("visualBriefings/briefing/manifest.json", "{}");
        this.WriteDataFile("media-staging/operation/transcript.md", "temporary");
        this.WriteDataFile("databases/sqlite/rag-index.sqlite3", "the index while AI Studio writes to it");
        this.WriteDataFile("databases/vector_database/stores/store_1/segments/segment", "the store while AI Studio writes to it");

        WriteFile(Path.Combine(this.ConfigDirectory, "settings.json"), "the settings on the disk, with the objects of the organization");
        WriteFile(Path.Combine(this.IndexCopiesDirectory, "databases", "sqlite", "rag-index.sqlite3"), "the copy of the index");
        WriteFile(Path.Combine(this.IndexCopiesDirectory, "databases", "vector_database", "stores", "store_1", "segments", "segment"), "the copy of the store");
    }

    [TearDown]
    public void RemoveFolders()
    {
        if (Directory.Exists(this.testDirectory))
            Directory.Delete(this.testDirectory, true);
    }

    [Test]
    public async Task ABackupHoldsWhatBelongsIntoItAndNothingElse()
    {
        var archive = await this.WriteBackupAsync(this.IndexCopiesDirectory);

        Assert.That(archive.Keys, Is.EquivalentTo(new[]
        {
            "config/settings.json",
            $"data/workspaces/{WORKSPACE_ID}/name",
            $"data/workspaces/{WORKSPACE_ID}/{CHAT_ID}/thread.json",
            $"data/workspaces/{WORKSPACE_ID}/{CHAT_ID}/attachments/transcripts/meeting-transcript-0001.md",
            $"data/tempChats/{Guid.Empty}/thread.json",
            "data/plugins/my-assistant/plugin.lua",
            "data/plugins/my-assistant/.hidden.lua",
            "data/tokenizers/openai-gpt-6/tokenizer.json",
            "data/visualBriefings/briefing/manifest.json",
            "data/databases/sqlite/rag-index.sqlite3",
            "data/databases/vector_database/stores/store_1/segments/segment",
            DataBackupFormat.MANIFEST_ENTRY_NAME,
        }));

        Assert.Multiple(() =>
        {
            Assert.That(archive["config/settings.json"], Is.EqualTo(SETTINGS_JSON), "The settings come from memory, without the objects of configuration plugins.");
            Assert.That(archive["data/databases/sqlite/rag-index.sqlite3"], Is.EqualTo("the copy of the index"));
            Assert.That(archive["data/databases/vector_database/stores/store_1/segments/segment"], Is.EqualTo("the copy of the store"));
        });
    }

    [Test]
    public async Task WithoutCopiesTheBackupLeavesTheIndexesOut()
    {
        var archive = await this.WriteBackupAsync(string.Empty);

        Assert.That(archive.Keys.Where(name => name.StartsWith("data/databases/", StringComparison.Ordinal)), Is.Empty, "The indexes in the data folder change while AI Studio runs.");
    }

    [Test]
    public async Task TheManifestComesLastAndListsEveryOtherFile()
    {
        await using var target = new MemoryStream();
        await DataBackupWriter.WriteAsync(target, this.CreateSources(this.IndexCopiesDirectory), CreateManifest(), null, CancellationToken.None);
        target.Position = 0;

        using var zip = new ZipArchive(target, ZipArchiveMode.Read);
        var manifestEntry = zip.Entries[^1];
        var manifest = await ReadManifestAsync(manifestEntry);
        var otherEntries = zip.Entries.Take(zip.Entries.Count - 1).ToList();

        Assert.That(manifestEntry.FullName, Is.EqualTo(DataBackupFormat.MANIFEST_ENTRY_NAME), "Without the manifest, a backup was not written completely.");
        Assert.That(manifest.Entries.Select(entry => entry.Name), Is.EqualTo(otherEntries.Select(entry => entry.FullName)));

        foreach (var entry in otherEntries)
        {
            await using var stream = entry.Open();
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
            var listed = manifest.Entries.Single(listedEntry => listedEntry.Name == entry.FullName);

            Assert.Multiple(() =>
            {
                Assert.That(listed.Sha256, Is.EqualTo(hash), $"The hash of '{entry.FullName}' lets a restore notice a damaged backup.");
                Assert.That(listed.Size, Is.EqualTo(entry.Length));
            });
        }
    }

    /// <remarks>
    /// The classic ZIP format stores sizes and positions in 32 bits, which ends at 4 GiB. The index
    /// of large data sources gets there, so the backup has to switch to ZIP64 on its own: for the
    /// size of the entry, and for the position of every entry behind it, the manifest among them. The file is sparse, so it takes no space, but the backup takes about
    /// 4.5 GiB until the test ends.
    /// </remarks>
    [Test]
    [Explicit("Writes a backup of about 4.5 GiB, which takes about a minute and as much free space.")]
    public async Task ABackupBeyondFourGibibytesStaysReadable()
    {
        const long LARGE_FILE_SIZE = 4_608L * 1024 * 1024;
        const long FOUR_GIBIBYTES = 4L * 1024 * 1024 * 1024;

        // Stored without compression, like every video, so the backup grows beyond 4 GiB as well:
        var largeFilePath = Path.Combine(this.DataDirectory, "visualBriefings", "large", "recording.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(largeFilePath)!);
        await using (var largeFile = new FileStream(largeFilePath, FileMode.Create, FileAccess.Write))
            largeFile.SetLength(LARGE_FILE_SIZE);

        var archivePath = Path.Combine(this.testDirectory, "backup.mwbackup");
        await using (var target = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await DataBackupWriter.WriteAsync(target, this.CreateSources(string.Empty), CreateManifest(), null, CancellationToken.None);

        await using var archiveStream = File.OpenRead(archivePath);
        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var largeEntry = zip.GetEntry("data/visualBriefings/large/recording.mp4")!;
        var manifest = await ReadManifestAsync(zip.Entries[^1]);
        var listed = manifest.Entries.Single(entry => entry.Name == largeEntry.FullName);

        string hash;
        await using (var entryStream = largeEntry.Open())
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(entryStream));

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(archivePath).Length, Is.GreaterThan(FOUR_GIBIBYTES), "Only then do positions need ZIP64 as well.");
            Assert.That(largeEntry.Length, Is.EqualTo(LARGE_FILE_SIZE));
            Assert.That(listed.Size, Is.EqualTo(LARGE_FILE_SIZE));
            Assert.That(listed.Sha256, Is.EqualTo(hash), "The large file comes back as it was.");
            Assert.That(manifest.Entries, Has.Count.EqualTo(zip.Entries.Count - 1), "The manifest lies behind the large file, beyond 4 GiB, and is still found.");
        });
    }

    private async Task<Dictionary<string, string>> WriteBackupAsync(string indexCopiesDirectory)
    {
        await using var target = new MemoryStream();
        await DataBackupWriter.WriteAsync(target, this.CreateSources(indexCopiesDirectory), CreateManifest(), null, CancellationToken.None);
        target.Position = 0;

        using var zip = new ZipArchive(target, ZipArchiveMode.Read);
        var contents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            contents[entry.FullName] = await reader.ReadToEndAsync();
        }

        return contents;
    }

    private DataBackupSources CreateSources(string indexCopiesDirectory) => new(this.DataDirectory, this.ConfigDirectory, SETTINGS_JSON, indexCopiesDirectory, ["plugins/company-assistant/"]);

    private static DataBackupManifest CreateManifest() => new(DataBackupFormat.FORMAT_VERSION, "26.10.1", "V6", DateTimeOffset.UtcNow, "/data", "/config", true, []);

    private static async Task<DataBackupManifest> ReadManifestAsync(ZipArchiveEntry manifestEntry)
    {
        await using var stream = manifestEntry.Open();
        return (await JsonSerializer.DeserializeAsync<DataBackupManifest>(stream, DataBackupFormat.JSON_OPTIONS))!;
    }

    private void WriteDataFile(string relativePath, string content) => WriteFile(Path.Combine(this.DataDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)), content);

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}