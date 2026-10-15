using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a file a configuration plugin names in a setting is found, e.g., the PowerPoint
/// template it ships for the Slide Planner.
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
}