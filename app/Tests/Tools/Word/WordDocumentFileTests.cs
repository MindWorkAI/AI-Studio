using AIStudio.Tools.Word;

using DocumentFormat.OpenXml.Packaging;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Word;

/// <summary>
/// Checks that a document comes about without anything next to the file the user chose, and
/// without touching the template.
/// </summary>
/// <remarks>
/// Inside the Flatpak, the save dialog grants access to exactly the chosen file. A temporary file next
/// to it, or a file moved there, fails, which no test on another system would notice otherwise.
/// </remarks>
[TestFixture]
public sealed class WordDocumentFileTests
{
    private const string MARKDOWN = "# Mars\n\nClose to Earth.";

    private static readonly byte[] PREVIOUS_CONTENT = [1, 2, 3];

    private string templateFolder = string.Empty;
    private string targetFolder = string.Empty;

    [SetUp]
    public void CreateFolders()
    {
        var testFolder = Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"));
        this.templateFolder = Directory.CreateDirectory(Path.Join(testFolder, "templates")).FullName;
        this.targetFolder = Directory.CreateDirectory(Path.Join(testFolder, "documents")).FullName;
    }

    [TearDown]
    public void RemoveFolders() => Directory.Delete(Path.GetDirectoryName(this.templateFolder)!, true);

    [Test]
    public async Task TheDocumentGoesIntoTheChosenFileOnly()
    {
        var templatePath = await this.CreateTemplateAsync();
        var templateContent = await File.ReadAllBytesAsync(templatePath);
        var targetPath = Path.Join(this.targetFolder, "Mars.docx");
        await File.WriteAllBytesAsync(targetPath, PREVIOUS_CONTENT);
        var temporaryFiles = TemporaryFiles();

        await WordDocumentFile.CreateAsync(templatePath, targetPath, MARKDOWN, NullLogger.Instance, CancellationToken.None);

        using var document = WordprocessingDocument.Open(targetPath, false);
        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFileSystemEntries(this.targetFolder), Is.EqualTo(new[] { targetPath }), "Nothing else came about next to the chosen file.");
            Assert.That(document.MainDocumentPart!.Document!.Body!.InnerText, Is.EqualTo("MarsClose to Earth."), "The chosen file holds the document instead of what it held before.");
            Assert.That(File.ReadAllBytes(templatePath), Is.EqualTo(templateContent), "The template stays as it is.");
            Assert.That(TemporaryFiles(), Is.EquivalentTo(temporaryFiles), "The copy of the template is gone.");
        });
    }

    [Test]
    public async Task AFileWhichIsNoWordDocumentLeavesTheChosenFileAlone()
    {
        var templatePath = Path.Join(this.templateFolder, "template.docx");
        await File.WriteAllBytesAsync(templatePath, [1, 2, 3, 4]);
        var targetPath = Path.Join(this.targetFolder, "Mars.docx");
        await File.WriteAllBytesAsync(targetPath, PREVIOUS_CONTENT);
        var temporaryFiles = TemporaryFiles();

        Assert.CatchAsync(() => WordDocumentFile.CreateAsync(templatePath, targetPath, MARKDOWN, NullLogger.Instance, CancellationToken.None));
        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(targetPath), Is.EqualTo(PREVIOUS_CONTENT));
            Assert.That(TemporaryFiles(), Is.EquivalentTo(temporaryFiles), "The copy of the template is gone.");
        });
    }

    [Test]
    public async Task TheTemplateItselfMayBeTheChosenFile()
    {
        var templatePath = await this.CreateTemplateAsync();

        await WordDocumentFile.CreateAsync(templatePath, templatePath, MARKDOWN, NullLogger.Instance, CancellationToken.None);

        using var document = WordprocessingDocument.Open(templatePath, false);
        Assert.That(document.MainDocumentPart!.Document!.Body!.InnerText, Is.EqualTo("MarsClose to Earth."), "The template was copied before it was written over.");
    }

    private async Task<string> CreateTemplateAsync()
    {
        var templatePath = Path.Join(this.templateFolder, "template.dotx");
        using var template = TestWordTemplate.Create(asTemplate: true);
        await File.WriteAllBytesAsync(templatePath, template.ToArray());
        return templatePath;
    }

    /// <summary>
    /// The files in the folder where the documents come about; others may have left some there.
    /// </summary>
    private static string[] TemporaryFiles()
    {
        var folder = Path.Join(Path.GetTempPath(), "mindwork-ai-studio-documents");
        return Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
    }
}