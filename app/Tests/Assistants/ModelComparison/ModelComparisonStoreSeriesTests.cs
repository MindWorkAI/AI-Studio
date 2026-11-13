using AIStudio.Assistants.ModelComparison;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the Model Comparison assistant stores its comparison series and their chats below the data directory.
/// </summary>
[TestFixture]
public sealed class ModelComparisonStoreSeriesTests
{
    private static readonly DateTimeOffset EARLIER = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LATER = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private string dataDirectory = string.Empty;

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-model-comparison-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task AStoredSeriesIsReadBackWithAllItsFields()
    {
        var series = Series(EARLIER);
        await this.CreateStore().CreateSeriesAsync(series);

        var loaded = await this.CreateStore().LoadSeriesAsync(series.Id);
        var task = loaded!.Tasks.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Name, Is.EqualTo(series.Name));
            Assert.That(loaded.First, Is.EqualTo(series.First));
            Assert.That(loaded.Judge, Is.EqualTo(series.Judge));
            Assert.That(loaded.ToolIds, Is.EqualTo(series.ToolIds));
            Assert.That(loaded.DataSourceIds, Is.EqualTo(series.DataSourceIds));
            Assert.That(loaded.RetrievalMode, Is.EqualTo(DataSourceRetrievalMode.SEMANTIC_SEARCH));
            Assert.That(task.State, Is.EqualTo(ModelComparisonSeriesTaskState.DONE));
            Assert.That(task.Vote, Is.EqualTo(ModelComparisonModelChoice.SECOND));
            Assert.That(task.ChatTwo.UserMessageCount, Is.EqualTo(3));
            Assert.That(task.ChatOfFirstModel, Is.EqualTo(task.ChatTwo), "The first model answered in the second chat.");
            Assert.That(task.ChatOne.Duration, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(task.IsBlind, Is.True);
        }
    }

    [Test]
    public async Task TheSeriesWorkedOnLastComesFirst()
    {
        var store = this.CreateStore();
        var older = Series(EARLIER);
        var newer = Series(LATER);
        await store.CreateSeriesAsync(older);
        await store.CreateSeriesAsync(newer);

        var series = await store.ListSeriesAsync();

        Assert.That(series.Select(entry => entry.Id), Is.EqualTo(new[] { newer.Id, older.Id }));
    }

    [Test]
    public async Task AChatOfASeriesIsStoredAndLoaded()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);

        var thread = new ChatThread { ChatId = Guid.NewGuid() };
        thread.Blocks.Add(new ContentBlock { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = "Summarize the report." } });
        await new ModelComparisonSeriesChatStore(store, series.Id).SaveThreadAsync(thread);

        var loaded = await new ModelComparisonSeriesChatStore(this.CreateStore(), series.Id).LoadThreadAsync(thread.ChatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded!.Blocks, Has.Count.EqualTo(1));
            Assert.That(((ContentText)loaded.Blocks[0].Content!).Text, Is.EqualTo("Summarize the report."));
            Assert.That(store.ReadAllSeriesChatLocations()[thread.ChatId], Is.EqualTo(series.Id));
        }
    }

    [Test]
    public async Task AChatWithoutAFileLoadsAsNone()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);

        Assert.That(await store.LoadSeriesChatAsync(series.Id, Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task AChatOfADeletedSeriesIsNotStored()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);
        await store.DeleteSeriesAsync(series.Id);

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.SaveSeriesChatAsync(series.Id, new ChatThread { ChatId = Guid.NewGuid() }));
        Assert.That(Directory.Exists(Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "series", series.Id.ToString("D"))), Is.False, "The series does not come back with a chat alone.");
    }

    [Test]
    public async Task SavingASeriesReplacesWhatWasStoredOfIt()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);

        series.Name = "Monthly reports";
        await store.SaveSeriesAsync(series);

        Assert.That((await this.CreateStore().LoadSeriesAsync(series.Id))!.Name, Is.EqualTo("Monthly reports"));
    }

    [Test]
    public async Task ADeletedSeriesDoesNotComeBackWhenItIsSaved()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);
        await store.DeleteSeriesAsync(series.Id);

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.SaveSeriesAsync(series));
        Assert.That(Directory.Exists(Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "series", series.Id.ToString("D"))), Is.False, "E.g., the verdict of the judge must not bring a deleted series back.");
    }

    [Test]
    public async Task DeletingASeriesDeletesItsChats()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);
        var thread = new ChatThread { ChatId = Guid.NewGuid() };
        await store.SaveSeriesChatAsync(series.Id, thread);

        await store.DeleteSeriesAsync(series.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.ListSeriesAsync(), Is.Empty);
            Assert.That(store.ReadAllSeriesChatLocations(), Is.Empty);
        }
    }

    [Test]
    public async Task DeletingAllSeriesDeletesTheirChatsButKeepsThePresetsAndTheResults()
    {
        var store = this.CreateStore();
        var first = Series(EARLIER);
        var second = Series(LATER);
        await store.CreateSeriesAsync(first);
        await store.CreateSeriesAsync(second);
        await store.SaveSeriesChatAsync(first.Id, new ChatThread { ChatId = Guid.NewGuid() });
        await store.SavePresetAsync(new ModelComparisonPreset { Id = Guid.NewGuid(), Name = "Weekly report" });
        await store.SaveResultAsync(new ModelComparisonRecord { Id = Guid.NewGuid() });

        var unreadableDirectory = Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "series", Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(unreadableDirectory);
        await File.WriteAllTextAsync(Path.Combine(unreadableDirectory, "series.json"), """{ "fieldOfANewerVersion": true }""");

        await store.DeleteAllSeriesAsync();

        var results = 0;
        await foreach (var _ in this.CreateStore().ReadResultsAsync())
            results++;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await this.CreateStore().ListSeriesAsync(), Is.Empty);
            Assert.That(store.ReadAllSeriesChatLocations(), Is.Empty);
            Assert.That(Directory.Exists(unreadableDirectory), Is.False, "Deleting all series takes the series this version cannot read along.");
            Assert.That(await this.CreateStore().ListPresetsAsync(), Has.Count.EqualTo(1));
            Assert.That(results, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task DeletingAllSeriesWithoutAnyDoesNothing()
    {
        await this.CreateStore().DeleteAllSeriesAsync();

        Assert.That(await this.CreateStore().ListSeriesAsync(), Is.Empty);
    }

    [Test]
    public async Task AChatThisVersionCannotOpenIsReportedAndKept()
    {
        var store = this.CreateStore();
        var series = Series(EARLIER);
        await store.CreateSeriesAsync(series);
        var chatId = Guid.NewGuid();
        var path = Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "series", series.Id.ToString("D"), "chats", $"{chatId:D}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ not json");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadSeriesChatAsync(series.Id, chatId));
        Assert.That(File.Exists(path), Is.True);
    }

    private ModelComparisonStore CreateStore() => new(NullLogger<ModelComparisonStore>.Instance, new ModelComparisonStorageOptions { DataDirectory = this.dataDirectory });

    private static ModelComparisonSeries Series(DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Weekly reports",
        CreatedAtUtc = createdAtUtc,
        FirstProviderId = "provider-a",
        SecondProviderId = "provider-b",
        First = new() { LLMProvider = LLMProviders.OPEN_AI, ModelId = "model-a", Label = "Model A" },
        Second = new() { LLMProvider = LLMProviders.MISTRAL, ModelId = "model-b", Label = "Model B" },
        JudgeProviderId = "provider-judge",
        Judge = new() { LLMProvider = LLMProviders.ANTHROPIC, ModelId = "judge", Label = "Judge" },
        ToolIds = ["web_search"],
        DataSourceIds = ["data-source-1"],
        RetrievalMode = DataSourceRetrievalMode.SEMANTIC_SEARCH,
        Tasks =
        [
            new()
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = createdAtUtc,
                FinishedAtUtc = createdAtUtc.AddMinutes(20),
                QuestionPreview = "Summarize the report.",
                FirstModelIsChatOne = false,
                ChatOne = new() { ChatId = Guid.NewGuid(), StartedAtUtc = createdAtUtc, FinishedAtUtc = createdAtUtc.AddMinutes(5), UserMessageCount = 2 },
                ChatTwo = new() { ChatId = Guid.NewGuid(), StartedAtUtc = createdAtUtc.AddMinutes(5), FinishedAtUtc = createdAtUtc.AddMinutes(15), UserMessageCount = 3 },
                State = ModelComparisonSeriesTaskState.DONE,
                Vote = ModelComparisonModelChoice.SECOND,
                JudgeChoice = ModelComparisonModelChoice.FIRST,
                JudgeReasoning = "The first chat was more precise.",
            },
        ],
    };
}