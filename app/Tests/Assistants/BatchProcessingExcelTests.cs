using AIStudio.Assistants.BatchProcessing;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks the results table of the batch processing assistant as an Excel workbook: what it writes
/// comes back when a run is continued, also after the user saved the workbook in Excel.
/// </summary>
[TestFixture]
public sealed class BatchProcessingExcelTests
{
    [Test]
    public void WhatIsWrittenIsReadBack()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["File", "Summary"],
            ["report.pdf", "A short report.\nWith a second line."],
            [Path.Combine("Case 1"), "1/2"],
            ["empty.md", ""],
            ["notes.md", "  Leading spaces and ümlauts, 😀"],
        ];

        using var stream = new MemoryStream();
        BatchProcessingExcel.Write(stream, rows);
        stream.Position = 0;

        Assert.That(BatchProcessingExcel.Read(stream), Is.EqualTo(rows));
    }

    [Test]
    public void AWorkbookSavedByExcelIsReadAsWell()
    {
        //
        // Excel moves the texts into shared strings and leaves out empty cells:
        //
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sharedStrings = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStrings.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("File")),
                new SharedStringItem(new Text("Result")),
                new SharedStringItem(new Text("report.pdf")));

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData(
                new Row(SharedCell("A1", 0), SharedCell("B1", 1)) { RowIndex = 1 },
                new Row(SharedCell("B2", 2)) { RowIndex = 2 }));

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Results" }));
        }

        stream.Position = 0;

        Assert.That(BatchProcessingExcel.Read(stream), Is.EqualTo(new[] { new[] { "File", "Result" }, new[] { "", "report.pdf" } }));
    }

    [Test]
    public void ATooLongAnswerIsCutToFitACell()
    {
        var cellText = BatchProcessingExcel.ToCellText(new string('a', BatchProcessingExcel.MAX_CELL_LENGTH + 100));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cellText, Has.Length.EqualTo(BatchProcessingExcel.MAX_CELL_LENGTH));
            Assert.That(cellText, Does.EndWith("[...]"));
        }
    }

    [Test]
    public void CharactersWhichXmlCannotHoldAreLeftOut()
    {
        Assert.That(BatchProcessingExcel.ToCellText("A\u0001B\u0008C 😀"), Is.EqualTo("ABC 😀"));
    }

    private static Cell SharedCell(string reference, int index) => new()
    {
        CellReference = reference,
        DataType = CellValues.SharedString,
        CellValue = new CellValue(index),
    };
}