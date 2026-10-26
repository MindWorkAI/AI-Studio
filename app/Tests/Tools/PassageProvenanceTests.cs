using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the passages of a complete answer move into the provenance store.
/// </summary>
/// <remarks>
/// The chat keeps where a passage was found, and the store what the AI read there. Whatever the
/// store cannot take stays in the chat itself, so the user can read it either way.
/// </remarks>
[TestFixture]
public sealed class PassageProvenanceTests
{
    private const string HANDBOOK_URL = "file:///C:/docs/handbook.pdf";

    private static readonly CancellationToken TOKEN = CancellationToken.None;

    [Test]
    public async Task TheTextsOfAPassageMoveIntoTheStore()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var answer = new ContentText { SourcePassages = [Passage($"{HANDBOOK_URL}#page=3", "The mixing console.", pageNumber: 3) with { SurroundingContent = ["Before.", "After."] }] };

        var allStored = await answer.StoreReceivedContentAsync(store.Client, chatId);
        var passage = answer.SourcePassages.Single();
        var entries = await store.Client.GetEntriesAsync([passage.ProvenanceId!.Value], TOKEN);
        var entry = entries[passage.ProvenanceId.Value];

        Assert.Multiple(() =>
        {
            Assert.That(allStored, Is.True);
            Assert.That(passage.Text, Is.Empty, "The chat keeps only the reference.");
            Assert.That(passage.SurroundingContent, Is.Empty);
            Assert.That(passage.PageNumber, Is.EqualTo(3), "Where the passage was found stays with the chat, so the list of sources draws without the store.");
            Assert.That(entry.Content, Is.EqualTo("The mixing console."));
            Assert.That(entry.SurroundingContent, Is.EqualTo(new[] { "Before.", "After." }));
            Assert.That(entry.Kind, Is.EqualTo(ProvenanceKind.DATA_SOURCE_PASSAGE));
        });
    }

    [Test]
    public async Task EveryPassageGetsAnEntryOfItsOwn()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var answer = new ContentText { SourcePassages = [Passage($"{HANDBOOK_URL}#page=3", "The mixing console."), Passage($"{HANDBOOK_URL}#page=4", "The amplifier.")] };

        await answer.StoreReceivedContentAsync(store.Client, Guid.NewGuid());
        var ids = answer.SourcePassages.Select(passage => passage.ProvenanceId).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.None.Null);
            Assert.That(ids, Is.Unique);
        });
    }

    [Test]
    public async Task APassageWithoutTextsIsLeftAsItIs()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var image = Passage($"{HANDBOOK_URL}#page=7", string.Empty) with { Category = RetrievalContentCategory.IMAGE, Type = RetrievalContentType.IMAGE_PHOTO };
        var answer = new ContentText { SourcePassages = [image] };

        var allStored = await answer.StoreReceivedContentAsync(store.Client, Guid.NewGuid());
        var chatsWithEntries = await store.Client.GetChatIdsAsync(TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(allStored, Is.True);
            Assert.That(answer.SourcePassages.Single().ProvenanceId, Is.Null, "The AI was only told about the image, so there is no text to keep.");
            Assert.That(chatsWithEntries, Is.Empty);
        });
    }

    [Test]
    public async Task AStoredPassageIsNotStoredTwice()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var answer = new ContentText { SourcePassages = [Passage(HANDBOOK_URL, "The mixing console.")] };

        await answer.StoreReceivedContentAsync(store.Client, chatId);
        var firstId = answer.SourcePassages.Single().ProvenanceId;
        await answer.StoreReceivedContentAsync(store.Client, chatId);

        await using var context = store.CreateContext();
        Assert.Multiple(() =>
        {
            Assert.That(answer.SourcePassages.Single().ProvenanceId, Is.EqualTo(firstId));
            Assert.That(context.Entries.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task WithoutAStoreThePassagesKeepTheirTexts()
    {
        var answer = new ContentText { SourcePassages = [Passage(HANDBOOK_URL, "The mixing console.") with { SurroundingContent = ["Before."] }] };

        var allStored = await answer.StoreReceivedContentAsync(new NoProvenanceStoreClient("SQLite", "not available"), Guid.NewGuid());
        var passage = answer.SourcePassages.Single();

        Assert.Multiple(() =>
        {
            Assert.That(allStored, Is.False, "The caller tells the user, who should know why the chat file stays large.");
            Assert.That(passage.ProvenanceId, Is.Null);
            Assert.That(passage.Text, Is.EqualTo("The mixing console."), "The chat stays readable without the store.");
            Assert.That(passage.SurroundingContent, Is.EqualTo(new[] { "Before." }));
        });
    }

    [Test]
    public void StoringNeverStopsTheAnswer()
    {
        // A passage without its text, as an ERI server sent one before its fields were checked:
        var answer = new ContentText { SourcePassages = [Passage(HANDBOOK_URL, null!)] };

        Assert.DoesNotThrowAsync(() => answer.StoreReceivedContentAsync(Guid.NewGuid()), "Completing the answer waits for this, and a throw left its chat running until the app restarted.");
    }

    [Test]
    public async Task AStoredPassageIsSavedWithItsReferenceOnly()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var answer = new ContentText { Text = "The console has 24 channels.", SourcePassages = [Passage($"{HANDBOOK_URL}#page=3", "The mixing console.", pageNumber: 3)] };
        await answer.StoreReceivedContentAsync(store.Client, Guid.NewGuid());

        var json = JsonSerializer.Serialize(answer, WorkspaceBehaviour.JSON_OPTIONS);
        var loaded = JsonSerializer.Deserialize<ContentText>(json, WorkspaceBehaviour.JSON_OPTIONS)!;
        using var document = JsonDocument.Parse(json);
        var storedPassage = document.RootElement.GetProperty("source_passages")[0];

        Assert.Multiple(() =>
        {
            Assert.That(storedPassage.GetProperty("provenance_id").GetGuid(), Is.EqualTo(answer.SourcePassages.Single().ProvenanceId));
            Assert.That(storedPassage.GetProperty("text").GetString(), Is.Empty, "The text lies in the store.");
            Assert.That(loaded.SourcePassages.Single().ProvenanceId, Is.EqualTo(answer.SourcePassages.Single().ProvenanceId));
        });
    }

    [Test]
    public void StoredPassagesOfOneSourceAreToldApartByTheirEntries()
    {
        var first = Passage(HANDBOOK_URL, string.Empty) with { ProvenanceId = Guid.NewGuid() };
        var second = Passage(HANDBOOK_URL, string.Empty) with { ProvenanceId = Guid.NewGuid() };
        List<SourcePassage> passages = [first];

        passages.MergePassages([second, first]);

        Assert.That(passages, Is.EqualTo(new[] { first, second }), "Without their texts, two stored passages of one page would otherwise count as one.");
    }

    [Test]
    public void TheKindFollowsWhatThePassageIs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Passage(HANDBOOK_URL, "text").GetProvenanceKind(), Is.EqualTo(ProvenanceKind.DATA_SOURCE_PASSAGE));
            Assert.That((Passage(HANDBOOK_URL, "text") with { Type = RetrievalContentType.TEXT_TRANSCRIPT }).GetProvenanceKind(), Is.EqualTo(ProvenanceKind.DATA_SOURCE_PASSAGE));
            Assert.That((Passage(HANDBOOK_URL, "text") with { Type = RetrievalContentType.TEXT_EMAIL }).GetProvenanceKind(), Is.EqualTo(ProvenanceKind.MAIL), "A mail is a mail, whether a mailbox data source or a tool handed it over.");
            Assert.That((Passage(HANDBOOK_URL, "text") with { Type = RetrievalContentType.TEXT_WEBSITE }).GetProvenanceKind(), Is.EqualTo(ProvenanceKind.WEB_PAGE));
        });
    }

    private static SourcePassage Passage(string sourceUrl, string text, int? pageNumber = null) => new()
    {
        SourceUrl = sourceUrl,
        DataSourceName = "Handbooks",
        Path = sourceUrl,
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Text = text,
        PageNumber = pageNumber,
    };
}