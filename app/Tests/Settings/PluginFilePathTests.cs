using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a file a configuration plugin names in a setting is found, e.g., the PowerPoint
/// template it ships for the Slide Assistant.
/// </summary>
[TestFixture]
public sealed class PluginFilePathTests
{
    private string pluginDirectory = string.Empty;

    [SetUp]
    public void CreatePluginDirectory()
    {
        this.pluginDirectory = Path.Combine(Path.GetTempPath(), $"plugin-file-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(this.pluginDirectory, "templates"));
        File.WriteAllBytes(Path.Combine(this.pluginDirectory, "Corporate.pptx"), [1]);
        File.WriteAllBytes(Path.Combine(this.pluginDirectory, "templates", "Corporate.potx"), [1]);
    }

    [TearDown]
    public void DeletePluginDirectory() => Directory.Delete(this.pluginDirectory, true);

    [Test]
    public void AFileOfThePluginIsFoundByItsRelativePath()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolve("Corporate.pptx", this.pluginDirectory, out var nextToPlugin, out var issue), Is.True, issue);
            Assert.That(nextToPlugin, Is.EqualTo(Path.Combine(Path.GetFullPath(this.pluginDirectory), "Corporate.pptx")));
            Assert.That(PluginFilePath.TryResolve("templates/Corporate.potx", this.pluginDirectory, out var inFolder, out issue), Is.True, issue);
            Assert.That(inFolder, Is.EqualTo(Path.Combine(Path.GetFullPath(this.pluginDirectory), "templates", "Corporate.potx")), "Both kinds of slashes work on every system.");
        });
    }

    [Test]
    public void AnEmptyOrAbsolutePathStaysAsItIs()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "somewhere", "Corporate.potx");

        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolve(string.Empty, this.pluginDirectory, out var empty, out _), Is.True, "An organization may decide on no file at all.");
            Assert.That(empty, Is.Empty);
            Assert.That(PluginFilePath.TryResolve(absolutePath, this.pluginDirectory, out var absolute, out _), Is.True, "A file on a network share is the organization's business.");
            Assert.That(absolute, Is.EqualTo(absolutePath));
        });
    }

    [Test]
    public void APathLeavingThePluginDirectoryIsRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolve("../Corporate.pptx", this.pluginDirectory, out var resolved, out var issue), Is.False);
            Assert.That(issue, Does.Contain(".."));
            Assert.That(resolved, Is.Empty);
            Assert.That(PluginFilePath.TryResolve("templates/../../Corporate.pptx", this.pluginDirectory, out _, out _), Is.False);
        });
    }

    [Test]
    public void AMissingFileIsRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolve("Missing.pptx", this.pluginDirectory, out var resolved, out var issue), Is.False);
            Assert.That(issue, Does.Contain("does not exist"));
            Assert.That(resolved, Is.Empty);
            Assert.That(PluginFilePath.TryResolve("Corporate.pptx", string.Empty, out _, out _), Is.False, "Without the plugin directory, a relative path means nothing.");
        });
    }

    [Test]
    public void APathWhichHasToStayInsideThePluginLeadsToNothingElse()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "somewhere", "Corporate.potx");

        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolveInside("templates\\Corporate.potx", this.pluginDirectory, out var inFolder, out var issue), Is.True, issue);
            Assert.That(inFolder, Is.EqualTo(Path.Combine(Path.GetFullPath(this.pluginDirectory), "templates", "Corporate.potx")));
            Assert.That(PluginFilePath.TryResolveInside(absolutePath, this.pluginDirectory, out var absolute, out issue), Is.False, "Where only a file of the plugin will do, an absolute path is no way out.");
            Assert.That(issue, Does.Contain("outside"));
            Assert.That(absolute, Is.Empty);
            Assert.That(PluginFilePath.TryResolveInside(string.Empty, this.pluginDirectory, out _, out _), Is.False, "An empty path names no file.");
        });
    }

    [Test]
    public void AFolderOfThePluginIsFoundByItsRelativePath()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "somewhere", "Pictures");

        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolveDirectory("templates", this.pluginDirectory, out var inPlugin, out var issue), Is.True, issue);
            Assert.That(inPlugin, Is.EqualTo(Path.Combine(Path.GetFullPath(this.pluginDirectory), "templates")));
            Assert.That(PluginFilePath.TryResolveDirectory(string.Empty, this.pluginDirectory, out var empty, out _), Is.True, "An organization may decide on no folder at all.");
            Assert.That(empty, Is.Empty);
            Assert.That(PluginFilePath.TryResolveDirectory(absolutePath, this.pluginDirectory, out var absolute, out _), Is.True, "A folder on a network share is the organization's business.");
            Assert.That(absolute, Is.EqualTo(absolutePath));
        });
    }

    [Test]
    public void AFolderFollowsTheRulesOfAFile()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PluginFilePath.TryResolveDirectory("templates/../..", this.pluginDirectory, out var resolved, out var issue), Is.False);
            Assert.That(issue, Does.Contain(".."));
            Assert.That(resolved, Is.Empty);
            Assert.That(PluginFilePath.TryResolveDirectory("Corporate.pptx", this.pluginDirectory, out _, out issue), Is.False, "A file is no folder.");
            Assert.That(issue, Does.Contain("does not exist"));
            Assert.That(PluginFilePath.TryResolve("templates", this.pluginDirectory, out _, out _), Is.False, "A folder is no file.");
        });
    }
}