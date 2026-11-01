using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AIStudio.Assistants.ERI;
using AIStudio.Tools;

using Changelog = AIStudio.Components.Changelog;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks that the app reads the files below wwwroot which it shows itself, and that the release
/// can serve every file which the page of the app loads.
/// </summary>
/// <remarks>
/// The changelogs and the ERI specifications come from the files embedded into the app, not from a
/// request to its own server. A file which is missing there would leave the changelog or the ERI
/// assistant empty without any error. The release serves its static files from the same embedded
/// files; one which is missing there leaves the window without styles, or without any interaction
/// at all, while a debug build keeps working.
/// </remarks>
[TestFixture]
public sealed class WebRootFilesTests
{
    private static IEnumerable<string> ChangelogFiles() => Changelog.LOGS.Select(log => log.Filename);

    private static IEnumerable<ERIVersion> SpecificationVersions() => Enum.GetValues<ERIVersion>().Where(version => version.WasSpecificationSelected());

    private static string ReadAppPageSource([CallerFilePath] string sourceFilePath = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio", "App.razor"));

    /// <summary>
    /// Reads the scripts and style sheets which App.razor loads from the app itself, so that a new one
    /// is checked without anybody having to add it to a test.
    /// </summary>
    private static IEnumerable<string> FilesOfTheAppPage() => Regex.Matches(ReadAppPageSource(), @"<(?:script|link)\b[^>]*\b(?:src|href)=""(?<path>[^""]+)""")
        .Select(match => match.Groups["path"].Value)
        .Where(path => !path.Contains("//", StringComparison.Ordinal));

    [Test]
    public void TheAppPageLoadsFilesOfTheApp()
    {
        Assert.That(FilesOfTheAppPage(), Does.Contain("_framework/blazor.web.js").And.Contain("app.css"), "The test no longer finds the scripts and style sheets in App.razor.");
    }

    [TestCaseSource(nameof(FilesOfTheAppPage))]
    public void EveryFileOfTheAppPageIsEmbedded(string path)
    {
        var file = new WebRootFiles().FileProvider.GetFileInfo(path);
        Assert.That(file.Exists, Is.True, $"The release would not serve '{path}', since it is not among the embedded files.");
        Assert.That(file.Length, Is.GreaterThan(0), $"The release would serve '{path}' as an empty file.");
    }

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copied, Is.False);
            Assert.That(File.Exists(destinationPath), Is.False);
        }
    }
}