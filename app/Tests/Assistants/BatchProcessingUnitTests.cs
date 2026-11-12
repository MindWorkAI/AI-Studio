using AIStudio.Assistants.BatchProcessing;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks how the batch processing assistant groups the selected files into the units it sends to
/// the model: a file of the first level on its own, a subfolder with everything below it as one.
/// </summary>
[TestFixture]
public sealed class BatchProcessingUnitTests
{
    private static readonly string INPUT_FOLDER = Path.Combine(Path.GetTempPath(), "ai-studio-batch-units", "Documents");

    [Test]
    public void FilesOfTheFirstLevelAreUnitsOfTheirOwn()
    {
        var report = Path.Combine(INPUT_FOLDER, "report.pdf");
        var notes = Path.Combine(INPUT_FOLDER, "notes.md");

        var units = BatchProcessingUnit.Group(INPUT_FOLDER, [report, notes]);

        Assert.That(units, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(units.Select(unit => unit.RelativePath), Is.EqualTo(new[] { "notes.md", "report.pdf" }), "The units are sorted by name.");
            Assert.That(units.All(unit => !unit.IsFolder), Is.True);
            Assert.That(units[1].Files, Is.EqualTo(new[] { report }));
        }
    }

    [Test]
    public void ASubfolderMergesEverythingBelowIt()
    {
        var contract = Path.Combine(INPUT_FOLDER, "Case 1", "contract.pdf");
        var letter = Path.Combine(INPUT_FOLDER, "Case 1", "Mail", "letter.docx");
        var photo = Path.Combine(INPUT_FOLDER, "Case 1", "Mail", "Scans", "photo.png");

        var units = BatchProcessingUnit.Group(INPUT_FOLDER, [photo, contract, letter]);

        Assert.That(units, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(units[0].IsFolder, Is.True);
            Assert.That(units[0].RelativePath, Is.EqualTo("Case 1"));
            Assert.That(units[0].Path, Is.EqualTo(Path.Join(INPUT_FOLDER, "Case 1")));
            Assert.That(units[0].Files, Is.EqualTo(new[] { contract, letter, photo }), "The files of a subfolder are sorted by their path.");
        }
    }

    [Test]
    public void FilesAndSubfoldersOfTheFirstLevelMix()
    {
        var report = Path.Combine(INPUT_FOLDER, "report.pdf");
        var firstCase = Path.Combine(INPUT_FOLDER, "Case 1", "contract.pdf");
        var secondCase = Path.Combine(INPUT_FOLDER, "Case 2", "contract.pdf");

        var units = BatchProcessingUnit.Group(INPUT_FOLDER, [report, secondCase, firstCase]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(units.Select(unit => unit.RelativePath), Is.EqualTo(new[] { "Case 1", "Case 2", "report.pdf" }));
            Assert.That(units.Select(unit => unit.IsFolder), Is.EqualTo(new[] { true, true, false }));
        }
    }

    [Test]
    public void AFolderNameWithADotStaysWhole()
    {
        var file = Path.Combine(INPUT_FOLDER, "Project.v2", "plan.md");

        var units = BatchProcessingUnit.Group(INPUT_FOLDER, [file]);

        Assert.That(units.Single().RelativePath, Is.EqualTo("Project.v2"));
    }
}