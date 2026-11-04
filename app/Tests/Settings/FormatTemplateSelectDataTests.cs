using AIStudio.Settings;
using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which entries a selection of format templates offers, e.g., the one of the Slide Assistant.
/// </summary>
/// <remarks>
/// The choice is the path of a file. Whatever path is chosen now has to stay among the entries, also
/// when no format template names it, or the selection would change it behind the back of the user.
/// </remarks>
[TestFixture]
public sealed class FormatTemplateSelectDataTests
{
    private static readonly FormatTemplate REPORT = TemplateAt("Report", "Report.dotx");
    private static readonly FormatTemplate SLIDES = TemplateAt("Slides", "Slides.potx");
    private static readonly FormatTemplate LECTURE = TemplateAt("Lecture", "Lecture.pptx");
    private static readonly FormatTemplate[] FORMAT_TEMPLATES = [REPORT, SLIDES, LECTURE];

    [Test]
    public void NoTemplateComesFirstAndThenTheTemplatesOfTheKind()
    {
        var entries = ConfigurationSelectDataFactory.GetFormatTemplatesData(FORMAT_TEMPLATES, FileTypes.POWER_POINT_TEMPLATE, string.Empty).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries.Select(entry => entry.Value), Is.EqualTo(new[] { string.Empty, SLIDES.FilePath, LECTURE.FilePath }), "The Word template serves no presentation.");
            Assert.That(entries.Skip(1).Select(entry => entry.Name), Is.EqualTo(new[] { "Slides", "Lecture" }));
        }
    }

    [Test]
    public void AChosenTemplateIsListedOnce() => Assert.That(ConfigurationSelectDataFactory.GetFormatTemplatesData(FORMAT_TEMPLATES, FileTypes.POWER_POINT_TEMPLATE, SLIDES.FilePath).Count(entry => entry.Value == SLIDES.FilePath), Is.EqualTo(1));

    [Test]
    public void APathWhichNoTemplateNamesStaysAnEntryOfItsOwn()
    {
        var path = Path.Combine(Path.GetTempPath(), "plugin", "Corporate.potx");
        var entries = ConfigurationSelectDataFactory.GetFormatTemplatesData(FORMAT_TEMPLATES, FileTypes.POWER_POINT_TEMPLATE, path).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[^1].Value, Is.EqualTo(path));
            Assert.That(entries[^1].Name, Is.EqualTo("Corporate.potx"), "Named after its file.");
        }
    }

    [Test]
    public void TwoTemplatesOfTheSameFileAreListedOnce()
    {
        var copy = SLIDES with { Id = Guid.NewGuid().ToString(), Name = "Slides again" };
        var entries = ConfigurationSelectDataFactory.GetFormatTemplatesData([SLIDES, copy], FileTypes.POWER_POINT_TEMPLATE, string.Empty).ToList();

        Assert.That(entries.Select(entry => entry.Value), Is.EqualTo(new[] { string.Empty, SLIDES.FilePath }), "A selection needs distinct values.");
    }

    private static FormatTemplate TemplateAt(string name, string fileName) => new(1, Guid.NewGuid().ToString(), name, Path.Combine(Path.GetTempPath(), "templates", fileName));
}