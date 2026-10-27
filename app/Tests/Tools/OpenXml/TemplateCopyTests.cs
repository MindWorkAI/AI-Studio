using AIStudio.Tools.OpenXml;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.OpenXml;

/// <summary>
/// Checks that a copy of a template leaves nothing behind in the temporary folder.
/// </summary>
/// <remarks>
/// Writing a document into the copy and the copy into the chosen file is checked by the tests of the
/// documents themselves, e.g., PresentationFileTests.
/// </remarks>
[TestFixture]
public sealed class TemplateCopyTests
{
    private static readonly byte[] TEMPLATE_CONTENT = [1, 2, 3];

    private string testFolder = string.Empty;
    private string temporaryDirectory = string.Empty;

    [SetUp]
    public void CreateFolders()
    {
        this.testFolder = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "mindwork-ai-studio-tests", Guid.NewGuid().ToString("N"))).FullName;
        this.temporaryDirectory = $"mindwork-ai-studio-tests-{Guid.NewGuid():N}";
    }

    [TearDown]
    public void RemoveFolders()
    {
        Directory.Delete(this.testFolder, true);
        var copies = Path.Join(Path.GetTempPath(), this.temporaryDirectory);
        if (Directory.Exists(copies))
            Directory.Delete(copies, true);
    }

    [Test]
    public async Task TheCopyHoldsTheTemplateUntilItIsDisposed()
    {
        var templatePath = Path.Join(this.testFolder, "template.docx");
        await File.WriteAllBytesAsync(templatePath, TEMPLATE_CONTENT);

        string copyPath;
        using (var copy = await TemplateCopy.CreateAsync(templatePath, this.temporaryDirectory, ".docx", NullLogger.Instance, CancellationToken.None))
        {
            copyPath = copy.FilePath;
            Assert.Multiple(() =>
            {
                Assert.That(Path.GetDirectoryName(copyPath), Is.EqualTo(Path.Join(Path.GetTempPath(), this.temporaryDirectory)), "The copy comes about in the temporary folder, never next to the chosen file.");
                Assert.That(Path.GetExtension(copyPath), Is.EqualTo(".docx"));
                Assert.That(File.ReadAllBytes(copyPath), Is.EqualTo(TEMPLATE_CONTENT));
            });
        }

        Assert.That(File.Exists(copyPath), Is.False, "Disposing the copy removes it.");
    }

    [Test]
    public void AMissingTemplateLeavesNoCopyBehind()
    {
        var templatePath = Path.Join(this.testFolder, "missing.docx");

        Assert.ThrowsAsync<FileNotFoundException>(() => TemplateCopy.CreateAsync(templatePath, this.temporaryDirectory, ".docx", NullLogger.Instance, CancellationToken.None));
        Assert.That(Directory.GetFiles(Path.Join(Path.GetTempPath(), this.temporaryDirectory)), Is.Empty);
    }
}