using AIStudio.Assistants.BatchProcessing;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks which files the search of the batch processing assistant leaves out because a batch run
/// wrote them.
/// </summary>
/// <remarks>
/// A run writes everything directly into its output folder. The search comes across any of it only
/// when that folder lies below the input folder or is the input folder itself. An output folder
/// above the input folder once made the search leave out every document.
/// </remarks>
[TestFixture]
public sealed class BatchProcessingOutputFolderTests
{
    private const string RESULTS_FILE_NAME = "batch-results.csv";

    private static readonly string DESKTOP = Path.Combine(Path.GetTempPath(), "ai-studio-batch-output-folder", "Desktop");
    private static readonly string PROJECT_FOLDER = Path.Combine(DESKTOP, "Batch");
    private static readonly string INPUT_FOLDER = Path.Combine(PROJECT_FOLDER, "Documents");

    [Test]
    public void AnOutputFolderAboveTheInputFolderLeavesOutNoDocument()
    {
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, PROJECT_FOLDER, RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "report.pdf")), Is.False);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "notes", "minutes.md")), Is.False);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "log.csv")), Is.False, "The log of a run lies in the output folder, so a file of this name in the input folder is a document.");
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "notes")), Is.True);
        });
    }

    [Test]
    public void AnOutputFolderSeveralLevelsAboveTheInputFolderLeavesOutNoDocument()
    {
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, DESKTOP, RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "report.pdf")), Is.False);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "notes", "minutes.md")), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "notes")), Is.True);
        });
    }

    [Test]
    public void TheDefaultOutputFolderBelowTheInputFolderIsLeftOut()
    {
        var resultsFolder = Path.Combine(INPUT_FOLDER, "ai-results");
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, resultsFolder, RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(resultsFolder, "report_result.md")), Is.True);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "report.pdf")), Is.False);
            Assert.That(outputFolder.Enters(resultsFolder), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "notes")), Is.True);
        });
    }

    [Test]
    public void AnOutputFolderDeepBelowTheInputFolderIsLeftOutAlone()
    {
        var parentFolder = Path.Combine(INPUT_FOLDER, "2026", "october");
        var resultsFolder = Path.Combine(parentFolder, "results");
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, resultsFolder, RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(resultsFolder, "report_result.md")), Is.True);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(parentFolder, "report.pdf")), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "2026")), Is.True);
            Assert.That(outputFolder.Enters(parentFolder), Is.True);
            Assert.That(outputFolder.Enters(resultsFolder), Is.False);
        });
    }

    [Test]
    public void ADocumentWhoseNameBeginsLikeTheOutputFolderIsNoOutput()
    {
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, Path.Combine(INPUT_FOLDER, "out"), RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "output-notes.md")), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "output")), Is.True);
        });
    }

    [Test]
    public void AnOutputFolderNextToTheInputFolderWithTheSameBeginningLeavesOutNothing()
    {
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, $"{INPUT_FOLDER}-results", RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "report.pdf")), Is.False);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "log.csv")), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "notes")), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TheInputFolderAsOutputFolderLeavesOutTheLogAndTheResultsTable(bool withTrailingSeparator)
    {
        var outputDirectory = withTrailingSeparator ? INPUT_FOLDER + Path.DirectorySeparatorChar : INPUT_FOLDER;
        var outputFolder = new BatchProcessingOutputFolder(INPUT_FOLDER, outputDirectory, RESULTS_FILE_NAME);

        Assert.Multiple(() =>
        {
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "log.csv")), Is.True);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, RESULTS_FILE_NAME)), Is.True);
            Assert.That(outputFolder.IsOutputOfRun(Path.Combine(INPUT_FOLDER, "report.pdf")), Is.False);
            Assert.That(outputFolder.Enters(Path.Combine(INPUT_FOLDER, "notes")), Is.True, "Leaving out the output folder would leave out the input folder, too.");
        });
    }
}