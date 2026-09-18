using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the page a passage came from is handed on as a number.
/// </summary>
/// <remarks>
/// The runtime states the page of every page it reads. That number used to be written into the
/// text as a heading and read back out of it further down, which left Word and OpenDocument files
/// without a page for good: they are marked with a comment, not with a heading, so the search for
/// a heading never found anything. The tests here pin the number to the metadata, which is the one
/// place it is actually stated.
/// </remarks>
[TestFixture]
public sealed class ContentStreamPageNumberTests
{
    [Test]
    public void APdfPageStatesItsNumber()
    {
        var processed = ContentStreamSseHandler.ProcessEvent(PdfEvent(7, "The mixing console is described here."));

        Assert.Multiple(() =>
        {
            Assert.That(processed.PageNumber, Is.EqualTo(7), "The page comes from the metadata of the event.");
            Assert.That(processed.Content, Does.Contain("# Page 7"), "The heading stays, because it is what tells the AI which page it reads.");
        });
    }

    [Test]
    public void APdfPageWithoutANumberStatesNone()
    {
        var processed = ContentStreamSseHandler.ProcessEvent(PdfEvent(null, "A page the runtime could not number."));

        Assert.That(processed.PageNumber, Is.Null, "Without a number in the metadata there is no page to state.");
    }

    /// <remarks>
    /// This is the case the old approach got wrong: a document which writes about page numbers
    /// looks exactly like the marker that used to be searched for.
    /// </remarks>
    [Test]
    public void ATextWhichReadsLikeAPageMarkerIsNotOne()
    {
        var processed = ContentStreamSseHandler.ProcessEvent(new()
        {
            Content = "# Page 42\nStill nothing but the text of the document.",
            StreamId = NewStreamId(),
            Metadata = new ContentStreamTextMetadata(),
        });

        Assert.Multiple(() =>
        {
            Assert.That(processed.PageNumber, Is.Null, "Nothing is read out of the text, so a line which looks like a marker stays text.");
            Assert.That(processed.Content, Is.EqualTo("# Page 42\nStill nothing but the text of the document."), "The text itself is passed on untouched.");
        });
    }

    /// <remarks>
    /// A Word or OpenDocument page is held back until it is clear that no image follows it, so the
    /// page leaving the reader is always the one before the event which released it. Its number has
    /// to wait together with it; handing out the number of the arriving event would put every
    /// passage one page too far ahead.
    /// </remarks>
    [Test]
    public void ADocumentPageCarriesItsOwnNumberAndNotTheOneWhichReleasedIt()
    {
        var streamId = NewStreamId();
        try
        {
            var first = ContentStreamSseHandler.ProcessEvent(DocumentEvent(streamId, 1, "What the first page says."));
            var second = ContentStreamSseHandler.ProcessEvent(DocumentEvent(streamId, 2, "What the second page says."));

            Assert.Multiple(() =>
            {
                Assert.That(first.Content, Is.Null, "The first page is still being buffered, so nothing is released yet.");
                Assert.That(second.PageNumber, Is.EqualTo(1), "What is released here is the first page, so it carries page one.");
                Assert.That(second.Content, Does.Contain("What the first page says."), "The content released belongs to the page whose number is stated.");
            });
        }
        finally
        {
            ContentStreamSseHandler.Clear(streamId);
        }
    }

    [Test]
    public void TheLastDocumentPageIsReleasedWithItsNumber()
    {
        var streamId = NewStreamId();
        ContentStreamSseHandler.ProcessEvent(DocumentEvent(streamId, 1, "What the first page says."));
        ContentStreamSseHandler.ProcessEvent(DocumentEvent(streamId, 2, "What the second page says."));

        var remainder = ContentStreamSseHandler.Clear(streamId);

        Assert.That(remainder, Is.Not.Null, "The reader always keeps its last page, so there is something left to release.");
        Assert.Multiple(() =>
        {
            Assert.That(remainder!.Value.PageNumber, Is.EqualTo(2), "The page kept back is the second one.");
            Assert.That(remainder.Value.Content, Does.Contain("What the second page says."), "The content released belongs to the page whose number is stated.");
        });
    }

    /// <remarks>
    /// A slide is not a page, and no program can be told to open one. Stating none is what later
    /// lets a click on such a source open the file and stop there.
    /// </remarks>
    [Test]
    public void ASlideStatesNoPage()
    {
        var processed = ContentStreamSseHandler.ProcessEvent(new()
        {
            Content = "What the third slide says.",
            StreamId = NewStreamId(),
            Metadata = new ContentStreamPresentationMetadata { Presentation = new() { SlideNumber = 3 } },
        }, extractImages: false);

        Assert.Multiple(() =>
        {
            Assert.That(processed.PageNumber, Is.Null, "A slide number is not a page number.");
            Assert.That(processed.Content, Does.Contain("# Slide 3"), "The heading stays, so the AI still knows which slide it reads.");
        });
    }

    [Test]
    public void ASpreadsheetRowStatesNoPage()
    {
        var processed = ContentStreamSseHandler.ProcessEvent(new()
        {
            Content = "| Console | Channels |",
            StreamId = NewStreamId(),
            Metadata = new ContentStreamSpreadsheetMetadata { Spreadsheet = new() { SheetName = "Inventory", RowNumber = 0 } },
        });

        Assert.That(processed.PageNumber, Is.Null, "A sheet has rows, not pages.");
    }

    private static ContentStreamSseEvent PdfEvent(int? pageNumber, string content) => new()
    {
        Content = content,
        StreamId = NewStreamId(),
        Metadata = new ContentStreamPdfMetadata { Pdf = new() { PageNumber = pageNumber } },
    };

    private static ContentStreamSseEvent DocumentEvent(string streamId, int pageNumber, string content) => new()
    {
        Content = content,
        StreamId = streamId,
        Metadata = new ContentStreamDocumentMetadata { Document = new() { PageNumber = pageNumber } },
    };

    //
    // The readers are kept in static tables keyed by the stream. A test which reuses an ID would
    // read the pages another test left behind.
    //
    private static string NewStreamId() => Guid.NewGuid().ToString();
}