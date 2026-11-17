using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how AI Studio reads the files which come with it.
/// </summary>
/// <remarks>
/// The tests run a development build, which reads the files from its output folder: the project file copies every
/// embedded file there as well.
/// </remarks>
[TestFixture]
public sealed class AppResourcesTests
{
    [Test]
    public async Task AFileOfTheAppIsRead()
    {
        var text = await AppResources.ReadTextAsync("Assistants/PromptOptimizer/prompting_guideline.md");

        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task BackslashesAndOuterSlashesAreAccepted()
    {
        var withSlashes = await AppResources.ReadTextAsync("Assistants/PromptOptimizer/prompting_guideline.md");
        var withBackslashes = await AppResources.ReadTextAsync("/Assistants\\PromptOptimizer\\prompting_guideline.md");

        Assert.That(withBackslashes, Is.EqualTo(withSlashes));
    }

    [Test]
    public async Task AMissingFileGivesAnEmptyText()
    {
        var text = await AppResources.ReadTextAsync("Assistants/PromptOptimizer/does-not-exist.md");

        Assert.That(text, Is.Empty);
    }

    [Test]
    public void AFolderOfTheAppListsItsFiles()
    {
        var provider = AppResources.FolderProvider("Assistants/PromptOptimizer");

        Assert.That(provider.GetDirectoryContents(string.Empty).Select(file => file.Name), Does.Contain("prompting_guideline.md"));
    }
}