using AIStudio.Assistants.SlideBuilder;

namespace AIStudio.Tests.Assistants.SlideBuilder;

/// <summary>
/// Verifies template checks which run before a provider receives any source material.
/// </summary>
[TestFixture]
public sealed class PowerPointTemplateAnalyzerTests
{
    private string temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        this.temporaryDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-pptx-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this.temporaryDirectory))
            Directory.Delete(this.temporaryDirectory, true);
    }

    [Test]
    public void ATemplateMustBeSelected()
    {
        var result = PowerPointTemplateAnalyzer.Analyze(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.Analysis, Is.Null);
            Assert.That(result.Issue, Is.EqualTo("Please select a PowerPoint template."));
        });
    }

    [Test]
    public void AnOpenDocumentPresentationIsNotAcceptedAsAPowerPointTemplate()
    {
        var path = Path.Combine(this.temporaryDirectory, "template.odp");
        File.WriteAllText(path, "not relevant");

        var result = PowerPointTemplateAnalyzer.Analyze(path);

        Assert.Multiple(() =>
        {
            Assert.That(result.Analysis, Is.Null);
            Assert.That(result.Issue, Is.EqualTo("The template must be a PowerPoint .pptx file."));
        });
    }

    [Test]
    public void ACorruptedPptxIsRejected()
    {
        var path = Path.Combine(this.temporaryDirectory, "template.pptx");
        File.WriteAllText(path, "not a package");

        var result = PowerPointTemplateAnalyzer.Analyze(path);

        Assert.Multiple(() =>
        {
            Assert.That(result.Analysis, Is.Null);
            Assert.That(result.Issue, Is.EqualTo("The selected file is not a readable PowerPoint presentation."));
        });
    }
}
