using AIStudio.Tools.Databases;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what the passage of a hit of a local data source names as its location.
/// </summary>
/// <remarks>
/// The index keeps where a chunk is as the runtime stated it: pages, a sheet and rows, or lines.
/// What a passage names follows what the user finds in the document: the lines of a CSV file are
/// the rows a spreadsheet program shows, and the lines of a transcript name nothing the user could
/// ever look at.
/// </remarks>
[TestFixture]
public sealed class LocalRetrievalLocationTests
{
    [Test]
    public void APageRangeIsKeptAndAnEndOnTheSamePageIsNoRange()
    {
        var pages = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(PageNumber: 3, EndPageNumber: 5), RetrievalContentType.TEXT_DOCUMENT, 2);
        var onePage = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(PageNumber: 3, EndPageNumber: 3), RetrievalContentType.TEXT_DOCUMENT, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((pages.PageNumber, pages.EndPageNumber), Is.EqualTo(((int?)3, (int?)5)));
            Assert.That((onePage.PageNumber, onePage.EndPageNumber), Is.EqualTo(((int?)3, (int?)null)));
        }
    }

    [Test]
    public void ARowOfASheetKeepsItsSheet()
    {
        var located = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(SheetName: "Budget", StartRow: 3, EndRow: 9), RetrievalContentType.TEXT_SPREADSHEET, 1);

        Assert.That((located.SheetName, located.StartRow, located.EndRow, located.StartLine), Is.EqualTo(("Budget", (int?)3, (int?)9, (int?)null)));
    }

    [Test]
    public void TheLinesOfATableAreItsRows()
    {
        var located = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(StartLine: 12, EndLine: 30), RetrievalContentType.TEXT_SPREADSHEET, 1);

        Assert.That((located.SheetName, located.StartRow, located.EndRow, located.StartLine, located.EndLine), Is.EqualTo(((string?)null, (int?)12, (int?)30, (int?)null, (int?)null)), "Line 12 of a CSV file is row 12 in a spreadsheet program, header row included.");
    }

    [Test]
    public void ATextNamesItsLines()
    {
        var lines = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(StartLine: 40, EndLine: 85), RetrievalContentType.TEXT_DOCUMENT, 4);
        var oneLine = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(StartLine: 40, EndLine: 40), RetrievalContentType.TEXT_DOCUMENT, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((lines.StartLine, lines.EndLine, lines.StartRow), Is.EqualTo(((int?)40, (int?)85, (int?)null)));
            Assert.That((oneLine.StartLine, oneLine.EndLine), Is.EqualTo(((int?)40, (int?)null)));
        }
    }

    [Test]
    public void ATranscriptNamesNoLines()
    {
        var located = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(StartLine: 1, EndLine: 9), RetrievalContentType.TEXT_TRANSCRIPT, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((located.StartLine, located.EndLine), Is.EqualTo(((int?)null, (int?)null)), "The user never sees the transcript, so its lines would point nowhere.");
            Assert.That(located.GetLocationLabel(), Is.EqualTo(DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(), RetrievalContentType.TEXT_TRANSCRIPT, 4).GetLocationLabel()), "It is named by its part, as before.");
        }
    }

    [Test]
    public void AChunkIndexedBeforeLocationsWereKeptNamesItsPart()
    {
        var located = DataSourceLocalRetrievalService.LocatePassage(new ChunkLocation(), RetrievalContentType.TEXT_SPREADSHEET, 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((located.PageNumber, located.SheetName, located.StartRow, located.StartLine), Is.EqualTo(((int?)null, (string?)null, (int?)null, (int?)null)));
            Assert.That(located.GetLocationRange(), Is.EqualTo(new LocationRange(4, 4)), "It keeps the label it had before.");
        }
    }
}