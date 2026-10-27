using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which exports a format template serves, which decides where the export menu lists it.
/// </summary>
[TestFixture]
public sealed class FormatTemplateTests
{
    [TestCase("Report.docx", Description = "A Word document can serve as a template.")]
    [TestCase("Report.dotx", Description = "A Word template becomes a document when exported into.")]
    [TestCase("REPORT.DOTX", Description = "The case of the extension does not matter.")]
    public void AWordTemplateServesTheExportToWord(string fileName)
    {
        var formatTemplate = TemplateAt(fileName);

        Assert.Multiple(() =>
        {
            Assert.That(formatTemplate.Serves(FileExportFormat.MICROSOFT_WORD), Is.True);
            Assert.That(formatTemplate.Serves(FileExportFormat.OPEN_DOCUMENT_TEXT), Is.False, "LibreOffice writes no Word template.");
            Assert.That(formatTemplate.Serves(FileExportFormat.HTML), Is.False);
        });
    }

    [TestCase("Slides.potx", Description = "A PowerPoint template serves presentations, not documents.")]
    [TestCase("Slides.pptx")]
    [TestCase("Report.pdf", Description = "A file of another kind serves nothing.")]
    public void AnyOtherFileServesNoExportToWord(string fileName) => Assert.That(TemplateAt(fileName).Serves(FileExportFormat.MICROSOFT_WORD), Is.False);

    private static FormatTemplate TemplateAt(string fileName) => new(1, Guid.NewGuid().ToString(), "Report", Path.Combine(Path.GetTempPath(), "templates", fileName));
}