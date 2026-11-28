using System.Collections.Concurrent;

using AIStudio.Components;
using AIStudio.Tools.Imports;
using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks the bookkeeping of the imports of web content, which go on while the user is elsewhere.
/// </summary>
/// <remarks>
/// The web content reader of an assistant goes away when the user leaves the assistant. While the reader
/// loaded and cleaned the page itself, its result went to the assistant which was already gone. The imports
/// here are stand-ins: the bookkeeping does not care how a page is loaded.
/// </remarks>
[TestFixture]
public sealed class WebContentImportServiceTests
{
    private const string URL = "https://example.org/report";

    private static readonly ImportOwner ASSISTANT = new(ImportOwnerKind.ASSISTANT, "TRANSLATION_ASSISTANT:test");

    private static readonly ImportTarget FIELD = new(ASSISTANT, "primary");

    private static readonly WebContentImportRequest REQUEST = new(URL, CleanContent: true, AIStudio.Settings.Provider.NONE);

    [Test]
    public async Task AnImportIsBusyUntilItEndsAndShowsItsStep()
    {
        var service = CreateService();
        var cleaning = NewSignal();
        var finish = NewSignal();
        var running = service.Start(FIELD, REQUEST, async reportStep =>
        {
            reportStep(ReadWebContentSteps.CLEANING);
            cleaning.SetResult();
            await finish.Task;
            return new(URL, "Cleaned text");
        });

        Assert.That(running, Is.Not.Null);
        await cleaning.Task;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.IsBusy(ASSISTANT), Is.True);
            Assert.That(service.GetSnapshot(FIELD)?.Step, Is.EqualTo(ReadWebContentSteps.CLEANING));
            Assert.That(service.GetSnapshot(FIELD)?.Url, Is.EqualTo(URL), "A reader which comes back shows the address being loaded.");
            Assert.That(service.GetSnapshot(FIELD)?.CleanContent, Is.True, "A reader which comes back shows that the page is being cleaned.");
            Assert.That(service.TryTakeResult(FIELD), Is.Null, "Nothing waits while the import runs.");
        }

        finish.SetResult();
        await running!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.IsBusy(ASSISTANT), Is.False);
            Assert.That(service.GetSnapshot(FIELD), Is.Null);
        }
    }

    [Test]
    public async Task AFieldStartsNoSecondImportWhileOtherFieldsLoadAtTheSameTime()
    {
        var service = CreateService();
        var otherReader = new ImportTarget(ASSISTANT, "second-reader");
        var otherAssistant = new ImportTarget(new(ImportOwnerKind.ASSISTANT, "LEGAL_CHECK_ASSISTANT:test"), "primary");
        var finish = NewSignal();

        var first = service.Start(FIELD, REQUEST, async _ => { await finish.Task; return new(URL, "First text"); });
        var second = service.Start(FIELD, REQUEST, _ => Task.FromResult(new WebContentImportResult(URL, "Second text")));
        var ofOtherReader = service.Start(otherReader, REQUEST, async _ => { await finish.Task; return new(URL, "Text of the other reader"); });
        var ofOtherAssistant = service.Start(otherAssistant, REQUEST, async _ => { await finish.Task; return new(URL, "Text of the other assistant"); });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null, "The field still loads its first page.");
            Assert.That(ofOtherReader, Is.Not.Null, "Another reader of the same assistant loads at the same time.");
            Assert.That(ofOtherAssistant, Is.Not.Null, "A reader of another assistant loads at the same time.");
            Assert.That(service.GetSnapshots(), Has.Count.EqualTo(3));
        }

        finish.SetResult();
        await Task.WhenAll(first!, ofOtherReader!, ofOtherAssistant!);
        Assert.That(service.TryTakeResult(FIELD)?.Markdown, Is.EqualTo("First text"));
    }

    [Test]
    public async Task TheResultWaitsForItsFieldAndArrivesOnce()
    {
        var service = CreateService();
        await service.Start(FIELD, REQUEST, _ => Task.FromResult(new WebContentImportResult(URL, "Loaded text")))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.TryTakeResult(new ImportTarget(ASSISTANT, "second-reader")), Is.Null, "Another field gets nothing.");
            Assert.That(service.TryTakeResult(FIELD)?.Markdown, Is.EqualTo("Loaded text"));
            Assert.That(service.TryTakeResult(FIELD), Is.Null, "The result arrives once.");
        }
    }

    [Test]
    public async Task AResetDiscardsWaitingResultsButNoRunningImport()
    {
        var service = CreateService();
        var otherReader = new ImportTarget(ASSISTANT, "second-reader");
        await service.Start(FIELD, REQUEST, _ => Task.FromResult(new WebContentImportResult(URL, "Loaded text")))!;
        var finish = NewSignal();
        var running = service.Start(otherReader, REQUEST, async _ => { await finish.Task; return new(URL, "Later text"); });

        service.ClearOwnerState(ASSISTANT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.TryTakeResult(FIELD), Is.Null, "The waiting result is gone.");
            Assert.That(service.GetSnapshot(otherReader), Is.Not.Null, "The running import goes on.");
        }

        finish.SetResult();
        await running!;
        Assert.That(service.TryTakeResult(otherReader)?.Markdown, Is.EqualTo("Later text"));
    }

    [Test]
    public async Task TheOwnerHearsAboutTheStartEachStepAndTheEnd()
    {
        var service = CreateService();
        var owners = new ConcurrentQueue<ImportOwner>();
        service.StateChanged += owners.Enqueue;

        await service.Start(FIELD, REQUEST, reportStep =>
        {
            reportStep(ReadWebContentSteps.PARSING);
            reportStep(ReadWebContentSteps.CLEANING);
            return Task.FromResult(new WebContentImportResult(URL, "Cleaned text"));
        })!;

        Assert.That(owners, Has.Count.EqualTo(4).And.All.EqualTo(ASSISTANT), "The start, two steps, and the end.");
    }

    [Test]
    public async Task AnImportWhichFailsSaysWhyAndFreesItsField()
    {
        var service = CreateService();
        await service.Start(FIELD, REQUEST, _ => throw new HttpRequestException("The host does not answer."))!;

        var result = service.TryTakeResult(FIELD);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result?.Issue, Is.EqualTo(WebContentImportIssue.LOAD_FAILED));
            Assert.That(result?.ErrorMessage, Is.EqualTo("The host does not answer."));
            Assert.That(result?.Url, Is.EqualTo(URL));
            Assert.That(service.IsBusy(ASSISTANT), Is.False);
        }

        var retry = service.Start(FIELD, REQUEST, _ => Task.FromResult(new WebContentImportResult(URL, "Loaded text")));
        Assert.That(retry, Is.Not.Null, "The field can load again.");
        await retry!;
    }

    /// <summary>
    /// The stand-in imports need none of the services which load a real page.
    /// </summary>
    private static WebContentImportService CreateService() => new(null!, null!, null!, null!, null!, NullLogger<WebContentImportService>.Instance);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}