using AIStudio.Assistants.BatchProcessing;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks how the batch processing assistant reads a table, which the runtime extracted for it, and
/// splits it into the rows or columns it sends to the model one by one.
/// </summary>
[TestFixture]
public sealed class BatchProcessingTableTests
{
    /// <summary>
    /// A workbook as the runtime hands it over: each sheet under a heading with its name, in a CSV
    /// fence, with cells quoted where their text needs it.
    /// </summary>
    private const string WORKBOOK = "\n# Customers\n```csv\nName,City\n\"Smith, John\",Berlin\n\"Two\nlines\",\"```\"\n```\n\n# Empty\n```csv\n```\n";

    [Test]
    public void AWorkbookIsReadSheetBySheet()
    {
        var sheets = BatchProcessingTable.ReadSheets(WORKBOOK, isCsvFile: false);

        Assert.That(sheets.Select(sheet => sheet.Name), Is.EqualTo(new[] { "Customers", "Empty" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheets[0].Rows, Has.Count.EqualTo(3));
            Assert.That(sheets[0].Rows[1], Is.EqualTo(new[] { "Smith, John", "Berlin" }), "A quoted comma stays within its cell.");
            Assert.That(sheets[0].Rows[2], Is.EqualTo(new[] { "Two\nlines", "```" }), "A quoted line break and a quoted fence stay within their cells.");
            Assert.That(sheets[1].Rows, Is.Empty);
        }
    }

    [Test]
    public void AWorkbookWithLineEndsOfWindowsIsReadAlike()
    {
        var sheets = BatchProcessingTable.ReadSheets(WORKBOOK.ReplaceLineEndings("\r\n"), isCsvFile: false);

        Assert.That(sheets[0].Rows[1], Is.EqualTo(new[] { "Smith, John", "Berlin" }));
    }

    [TestCase("```csv\nName;City\nSmith, John;Berlin\n```\n", TestName = "Semicolons, with a comma in a cell")]
    [TestCase("```csv\nName,City\n\"Smith, John\",Berlin\n```\n", TestName = "Commas, with a quoted comma in a cell")]
    [TestCase("```csv\nName\tCity\nSmith, John\tBerlin\n```\n", TestName = "Tabs")]
    public void ACsvFileIsReadWithItsSeparator(string content)
    {
        var sheets = BatchProcessingTable.ReadSheets(content, isCsvFile: true);

        Assert.That(sheets, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheets[0].Name, Is.Empty);
            Assert.That(sheets[0].Rows, Is.EqualTo(new[] { new[] { "Name", "City" }, new[] { "Smith, John", "Berlin" } }));
        }
    }

    [Test]
    public void ACsvFileWithoutAnySeparatorIsOneColumn()
    {
        var sheets = BatchProcessingTable.ReadSheets("```csv\nName\nSmith\n```\n", isCsvFile: true);

        Assert.That(sheets[0].Rows, Is.EqualTo(new[] { new[] { "Name" }, new[] { "Smith" } }));
    }

    [Test]
    public void EachFurtherRowIsOneEntryWithTheHeadingsOfItsColumns()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["Name", "City", ""],
            ["Smith", "Berlin", "VIP"],
            ["", " ", ""],
            ["Miller", "", "new"],
        ];

        var entries = BatchProcessingTable.SplitIntoEntries(rows, BatchProcessingTableDirection.ROWS);

        Assert.That(entries, Has.Count.EqualTo(2), "A row without any value is no entry.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].Index, Is.EqualTo(1));
            Assert.That(entries[0].Text, Is.EqualTo("Name: Smith\nCity: Berlin\nColumn C: VIP"), "A column without a heading is named by its letter.");
            Assert.That(entries[1].Index, Is.EqualTo(3), "The index counts the rows of the sheet, empty ones included.");
            Assert.That(entries[1].Text, Is.EqualTo("Name: Miller\nColumn C: new"), "An empty value is left out.");
        }
    }

    [Test]
    public void EachFurtherColumnIsOneEntryWithTheHeadingsOfItsRows()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["Question", "Alice", "Bob"],
            ["Age", "34"],
            ["", "yes", "no"],
        ];

        var entries = BatchProcessingTable.SplitIntoEntries(rows, BatchProcessingTableDirection.COLUMNS);

        Assert.That(entries, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].Index, Is.EqualTo(1));
            Assert.That(entries[0].Text, Is.EqualTo("Question: Alice\nAge: 34\nRow 3: yes"), "A row without a heading is named by its number.");
            Assert.That(entries[1].Index, Is.EqualTo(2));
            Assert.That(entries[1].Text, Is.EqualTo("Question: Bob\nRow 3: no"), "A short row has no value in this column.");
        }
    }

    [TestCase("report.csv", true)]
    [TestCase("report.TSV", true)]
    [TestCase("report.xlsx", false)]
    [TestCase("report.ods", false)]
    public void OnlyCsvAndTsvFilesAreReadWithoutSheets(string path, bool expected) => Assert.That(BatchProcessingTable.IsCsvFile(path), Is.EqualTo(expected));
}