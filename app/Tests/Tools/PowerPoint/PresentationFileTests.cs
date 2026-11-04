using AIStudio.Tools.PowerPoint;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.PowerPoint;

/// <summary>
/// Checks that a presentation file comes about without anything next to the file the user chose.
/// </summary>
/// <remarks>
/// Inside the Flatpak, the save dialog grants access to exactly the chosen file. A temporary file next
/// to it, or a file moved there, fails, which no test on another system would notice otherwise. The
/// test template reads as L1 "Title" and L2 "Content".
/// </remarks>
[TestFixture]
public sealed class PresentationFileTests
{
    private static readonly SlideDeckSpec DECK = new("Mars",
    [
        new SlideSpec("L1", false, "Mars", "A plan", [], [], [], null),
        new SlideSpec("L2", false, "Why Mars", string.Empty, [new SlideTextBlock([new SlideParagraph("Close to Earth", 0)])], [], [], null),
    ]);

    private static readonly PresentationWriteOptions OPTIONS = new(null, string.Empty, false);

    private static readonly byte[] PREVIOUS_CONTENT = [1, 2, 3];

    private string templateFolder = string.Empty;
    private string targetFolder = string.Empty;

    [SetUp]
    public void CreateFolders()
    {
        var testFolder = Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"));
        this.templateFolder = Directory.CreateDirectory(Path.Join(testFolder, "templates")).FullName;
        this.targetFolder = Directory.CreateDirectory(Path.Join(testFolder, "presentations")).FullName;
    }

    [TearDown]
    public void RemoveFolders() => Directory.Delete(Path.GetDirectoryName(this.templateFolder)!, true);

    [Test]
    public async Task ThePresentationGoesIntoTheChosenFileOnly()
    {
        var templatePath = await this.CreateTemplateAsync();
        var targetPath = Path.Join(this.targetFolder, "Mars.pptx");
        await File.WriteAllBytesAsync(targetPath, PREVIOUS_CONTENT);
        var temporaryFiles = TemporaryFiles();

        var result = await PresentationFile.CreateAsync(templatePath, targetPath, DECK, BasisOf(templatePath), OPTIONS, new NoPictures(), NullLogger.Instance, CancellationToken.None);

        using var document = PresentationDocument.Open(targetPath, false);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsWritten, Is.True);
            Assert.That(Directory.GetFileSystemEntries(this.targetFolder), Is.EqualTo(new[] { targetPath }), "Nothing else came about next to the chosen file.");
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(2), "The chosen file holds the presentation instead of what it held before.");
            Assert.That(TemporaryFiles(), Is.EquivalentTo(temporaryFiles), "The copy of the template is gone.");
        }
    }

    [Test]
    public async Task AChangedTemplateLeavesTheChosenFileAlone()
    {
        var templatePath = await this.CreateTemplateAsync();
        var targetPath = Path.Join(this.targetFolder, "Mars.pptx");
        await File.WriteAllBytesAsync(targetPath, PREVIOUS_CONTENT);
        var temporaryFiles = TemporaryFiles();

        // Without a basis, nothing is known to fit, as after a change of the template:
        var result = await PresentationFile.CreateAsync(templatePath, targetPath, DECK, new Dictionary<string, string>(), OPTIONS, new NoPictures(), NullLogger.Instance, CancellationToken.None);

        var targetContent = await File.ReadAllBytesAsync(targetPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsWritten, Is.False);
            Assert.That(targetContent, Is.EqualTo(PREVIOUS_CONTENT));
            Assert.That(TemporaryFiles(), Is.EquivalentTo(temporaryFiles), "The copy of the template is gone.");
        }
    }

    [Test]
    public async Task TheTemplateItselfMayBeTheChosenFile()
    {
        var templatePath = await this.CreateTemplateAsync();
        var basis = BasisOf(templatePath);

        var result = await PresentationFile.CreateAsync(templatePath, templatePath, DECK, basis, OPTIONS, new NoPictures(), NullLogger.Instance, CancellationToken.None);

        using var document = PresentationDocument.Open(templatePath, false);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsWritten, Is.True);
            Assert.That(document.PresentationPart!.SlideParts.Count(), Is.EqualTo(2), "The template was copied before it was written over.");
        }
    }

    private async Task<string> CreateTemplateAsync()
    {
        var templatePath = Path.Join(this.templateFolder, "template.pptx");
        using var template = TestTemplate.Create(
            new TestLayout("Title", TestPlaceholder.Title(), TestPlaceholder.Subtitle(new SlotBounds(1_524_000, 3_602_038, 9_144_000, 1_655_762))),
            new TestLayout("Content", TestPlaceholder.Title(), TestPlaceholder.Text(1, TestTemplate.MASTER_BODY_BOUNDS)));

        await File.WriteAllBytesAsync(templatePath, template.ToArray());
        return templatePath;
    }

    private static IReadOnlyDictionary<string, string> BasisOf(string templatePath) => PresentationTemplate.ReadFile(templatePath).DescribeBasisOf(DECK);

    /// <summary>
    /// The files in the folder where the presentations come about; others may have left some there.
    /// </summary>
    private static string[] TemporaryFiles()
    {
        var folder = Path.Join(Path.GetTempPath(), "mindwork-ai-studio-presentations");
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }

    private sealed class NoPictures : IPictureSource
    {
        public Task<SlidePicture?> LoadAsync(int number, CancellationToken token) => Task.FromResult<SlidePicture?>(null);
    }
}