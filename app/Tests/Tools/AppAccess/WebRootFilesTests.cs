using AIStudio.Assistants.ERI;
using AIStudio.Tools;

using Changelog = AIStudio.Components.Changelog;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks that the app reads the files below wwwroot which it shows itself.
/// </summary>
/// <remarks>
/// The changelogs and the ERI specifications come from the files embedded into the app, not from a
/// request to its own server. A file which is missing there would leave the changelog or the ERI
/// assistant empty without any error.
/// </remarks>
[TestFixture]
public sealed class WebRootFilesTests
{
    private static IEnumerable<string> ChangelogFiles() => Changelog.LOGS.Select(log => log.Filename);

    private static IEnumerable<ERIVersion> SpecificationVersions() => Enum.GetValues<ERIVersion>().Where(version => version.WasSpecificationSelected());

    [TestCaseSource(nameof(ChangelogFiles))]
    public async Task EveryChangelogCanBeRead(string filename)
    {
        var text = await new WebRootFiles().ReadTextAsync($"changelog/{filename}");
        Assert.That(text, Is.Not.Empty);
    }

    [TestCaseSource(nameof(SpecificationVersions))]
    public async Task EveryERISpecificationCanBeRead(ERIVersion version)
    {
        var text = await version.ReadSpecification(new WebRootFiles());
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task AMissingFileReadsAsEmptyText()
    {
        var text = await new WebRootFiles().ReadTextAsync("changelog/does-not-exist.md");
        Assert.That(text, Is.Empty);
    }

    [TestCaseSource(nameof(SpecificationVersions))]
    public async Task EveryERISpecificationCanBeSaved(ERIVersion version)
    {
        var webRootFiles = new WebRootFiles();
        var destinationPath = Path.Combine(Path.GetTempPath(), $"eri-specification-{Guid.NewGuid():N}.json");
        try
        {
            Assert.That(await webRootFiles.CopyToAsync(version.SpecificationURL(), destinationPath), Is.True);
            Assert.That(await File.ReadAllTextAsync(destinationPath), Is.EqualTo(await version.ReadSpecification(webRootFiles)));
        }
        finally
        {
            File.Delete(destinationPath);
        }
    }

    [Test]
    public async Task AMissingFileIsNotCopied()
    {
        var destinationPath = Path.Combine(Path.GetTempPath(), $"eri-specification-{Guid.NewGuid():N}.json");

        var copied = await new WebRootFiles().CopyToAsync("specs/eri/does-not-exist.json", destinationPath);

        Assert.Multiple(() =>
        {
            Assert.That(copied, Is.False);
            Assert.That(File.Exists(destinationPath), Is.False);
        });
    }
}