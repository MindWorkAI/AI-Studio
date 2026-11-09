using System.IO.Compression;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how plugin archives are unpacked, from an import or from the configuration server of an organization.
/// </summary>
/// <remarks>
/// AI Studio unpacks these archives itself rather than through ZipFile.ExtractToDirectory, which on Linux and
/// macOS would keep a backslash in an entry name as part of the file name. Archives packed on Windows by hand
/// or with another tool hold such names, and the plugin.lua inside would never be found.
/// </remarks>
[TestFixture]
public sealed class PluginArchiveTests
{
    private string testDirectory = string.Empty;

    [SetUp]
    public void CreateTestDirectory()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-plugin-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
    }

    [TearDown]
    public void DeleteTestDirectory()
    {
        if (Directory.Exists(this.testDirectory))
            Directory.Delete(this.testDirectory, true);
    }

    [Test]
    public async Task AnArchivePackedOnWindowsIsUnpackedIntoItsFolders()
    {
        var archivePath = await this.CreateArchiveAsync(new ArchiveEntry("O\\plugin.lua", "ID = \"o\""), new ArchiveEntry("O\\lib\\"), new ArchiveEntry("O\\lib\\helpers.lua", "return {}"));
        var destination = Path.Combine(this.testDirectory, "unpacked");

        await PluginArchive.ExtractAsync(archivePath, destination);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(Path.Combine(destination, "O", "plugin.lua")), Is.EqualTo("ID = \"o\""));
            Assert.That(await File.ReadAllTextAsync(Path.Combine(destination, "O", "lib", "helpers.lua")), Is.EqualTo("return {}"));
        }
    }

    [TestCase("../plugin.lua", TestName = "An entry above the destination")]
    [TestCase("/plugin.lua", TestName = "An entry with an absolute path")]
    public async Task AnEntryOutsideOfTheDestinationIsRefused(string entryName)
    {
        var archivePath = await this.CreateArchiveAsync(new ArchiveEntry(entryName, "ID = \"o\""));
        var destination = Path.Combine(this.testDirectory, "unpacked");

        await Assert.ThrowsAsync<InvalidDataException>(() => PluginArchive.ExtractAsync(archivePath, destination));
        Assert.That(File.Exists(Path.Combine(this.testDirectory, "plugin.lua")), Is.False);
    }

    [Test]
    public async Task ADirectoryEntryWithDataIsRefused()
    {
        var archivePath = await this.CreateArchiveAsync(new ArchiveEntry("O/", "ID = \"o\""));

        await Assert.ThrowsAsync<InvalidDataException>(() => PluginArchive.ExtractAsync(archivePath, Path.Combine(this.testDirectory, "unpacked")));
    }

    [Test]
    public async Task AnArchiveIsNotUnpackedAfterTheCancellation()
    {
        var archivePath = await this.CreateArchiveAsync(new ArchiveEntry("O/plugin.lua", "ID = \"o\""));
        var destination = Path.Combine(this.testDirectory, "unpacked");

        await Assert.CatchAsync<OperationCanceledException>(() => PluginArchive.ExtractAsync(archivePath, destination, new CancellationToken(canceled: true)));
        Assert.That(File.Exists(Path.Combine(destination, "O", "plugin.lua")), Is.False);
    }

    private async Task<string> CreateArchiveAsync(params ArchiveEntry[] entries)
    {
        var archivePath = Path.Combine(this.testDirectory, $"{Guid.NewGuid():N}.mwplugin");
        await using var archiveStream = File.Create(archivePath);
        await using var archive = await ZipArchive.CreateAsync(archiveStream, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: null);
        foreach (var archiveEntry in entries)
        {
            var entry = archive.CreateEntry(archiveEntry.Name);
            if (archiveEntry.Content is null)
                continue;

            await using var entryStream = await entry.OpenAsync();
            await using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync(archiveEntry.Content);
        }

        return archivePath;
    }

    /// <summary>
    /// An entry of a test archive: a file with its content, or a directory without one.
    /// </summary>
    private sealed record ArchiveEntry(string Name, string? Content = null);
}