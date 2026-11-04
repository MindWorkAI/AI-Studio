using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what a format template serves, which decides where the export menu and the Slide Assistant list it.
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(formatTemplate.Serves(FileExportFormat.MICROSOFT_WORD), Is.True);
            Assert.That(formatTemplate.Serves(FileExportFormat.OPEN_DOCUMENT_TEXT), Is.False, "LibreOffice writes no Word template.");
            Assert.That(formatTemplate.Serves(FileExportFormat.HTML), Is.False);
        }
    }

    [TestCase("Slides.potx", Description = "A PowerPoint template serves the Slide Assistant, not the export.")]
    [TestCase("Slides.pptx")]
    [TestCase("Report.pdf", Description = "A file of another kind serves nothing.")]
    public void AnyOtherFileServesNoExportToWord(string fileName) => Assert.That(TemplateAt(fileName).Serves(FileExportFormat.MICROSOFT_WORD), Is.False);

    [TestCase("Slides.potx", true)]
    [TestCase("Slides.pptx", true)]
    [TestCase("SLIDES.POTX", true, Description = "The case of the extension does not matter.")]
    [TestCase("Report.dotx", false, Description = "A Word template serves no presentation.")]
    [TestCase("Report.pdf", false)]
    public void APowerPointTemplateServesTheSlideAssistant(string fileName, bool serves) => Assert.That(TemplateAt(fileName).Serves(FileTypes.POWER_POINT_TEMPLATE), Is.EqualTo(serves));

    [Test]
    public void TheTemplatesOfAKindKeepTheOrderOfTheSettings()
    {
        FormatTemplate[] formatTemplates = [TemplateAt("Report.dotx"), TemplateAt("Slides.potx"), TemplateAt("Letter.docx"), TemplateAt("Lecture.pptx")];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(formatTemplates.Serving(FileTypes.POWER_POINT_TEMPLATE), Is.EqualTo(new[] { formatTemplates[1], formatTemplates[3] }));
            Assert.That(formatTemplates.Serving(FileTypes.MS_WORD_TEMPLATE), Is.EqualTo(new[] { formatTemplates[0], formatTemplates[2] }));
        }
    }

    private static FormatTemplate TemplateAt(string fileName) => new(1, Guid.NewGuid().ToString(), "Report", Path.Combine(Path.GetTempPath(), "templates", fileName));
}