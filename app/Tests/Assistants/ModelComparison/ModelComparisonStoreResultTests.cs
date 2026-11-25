using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the Model Comparison assistant stores the results of its batches below the data directory.
/// </summary>
[TestFixture]
public sealed class ModelComparisonStoreResultTests
{
    private string dataDirectory = string.Empty;

    private string ResultsDirectory => Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "results");

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
    public async Task AStoredComparisonIsReadBackWithAllItsFields()
    {
        var record = Record();
        await this.CreateStore().SaveResultAsync(record);

        var records = await ReadAllAsync(this.CreateStore());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].Id, Is.EqualTo(record.Id));
            Assert.That(records[0].FinishedAtUtc, Is.EqualTo(record.FinishedAtUtc));
            Assert.That(records[0].QuestionPreview, Is.EqualTo(record.QuestionPreview));
            Assert.That(records[0].First, Is.EqualTo(record.First));
            Assert.That(records[0].Second, Is.EqualTo(record.Second));
            Assert.That(records[0].Judge, Is.EqualTo(record.Judge));
            Assert.That(records[0].Runs, Is.EqualTo(record.Runs));
        }
    }

    [Test]
    public async Task StoringTheSameBatchAgainDoesNotCountItTwice()
    {
        var store = this.CreateStore();
        var record = Record();

        await store.SaveResultAsync(record);
        await store.SaveResultAsync(record with { QuestionPreview = "Summarize it again." });
        var records = await ReadAllAsync(store);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].QuestionPreview, Is.EqualTo("Summarize it again."));
        }
    }

    [Test]
    public async Task AFileThisVersionCannotReadIsSkippedButKept()
    {
        var store = this.CreateStore();
        await store.SaveResultAsync(Record());

        var unreadablePath = Path.Combine(this.ResultsDirectory, $"{Guid.NewGuid():D}.json");
        await File.WriteAllTextAsync(unreadablePath, """{ "fieldOfANewerVersion": true }""");

        var records = await ReadAllAsync(store);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(File.Exists(unreadablePath), Is.True);
        }
    }

    [Test]
    public async Task DeletingAllResultsKeepsThePresets()
    {
        var store = this.CreateStore();
        await store.SaveResultAsync(Record());
        await store.SaveResultAsync(Record());
        await store.SavePresetAsync(new ModelComparisonPreset { Id = Guid.NewGuid(), Name = "Weekly report" });

        var unreadablePath = Path.Combine(this.ResultsDirectory, $"{Guid.NewGuid():D}.json");
        await File.WriteAllTextAsync(unreadablePath, """{ "fieldOfANewerVersion": true }""");

        await store.DeleteAllResultsAsync();

        var records = await ReadAllAsync(this.CreateStore());
        var presets = await this.CreateStore().ListPresetsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(records, Is.Empty);
            Assert.That(File.Exists(unreadablePath), Is.False, "Deleting all results takes the files this version cannot read along.");
            Assert.That(presets, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task DeletingAllResultsWithoutAnyDoesNothing()
    {
        await this.CreateStore().DeleteAllResultsAsync();

        Assert.That(await ReadAllAsync(this.CreateStore()), Is.Empty);
    }

    [Test]
    public async Task NoStoredComparisonsReadAsNone()
    {
        var records = await ReadAllAsync(this.CreateStore());

        Assert.That(records, Is.Empty);
    }

    private ModelComparisonStore CreateStore() => new(NullLogger<ModelComparisonStore>.Instance, new ModelComparisonStorageOptions { DataDirectory = this.dataDirectory });

    private static async Task<List<ModelComparisonRecord>> ReadAllAsync(ModelComparisonStore store)
    {
        List<ModelComparisonRecord> records = [];
        await foreach (var record in store.ReadResultsAsync())
            records.Add(record);

        return records;
    }

    private static ModelComparisonRecord Record() => new()
    {
        Id = Guid.NewGuid(),
        FinishedAtUtc = new DateTimeOffset(2026, 11, 13, 9, 30, 0, TimeSpan.Zero),
        QuestionPreview = "Summarize it.",
        First = new() { LLMProvider = LLMProviders.OPEN_AI, ModelId = "first-model", Label = "First" },
        Second = new() { LLMProvider = LLMProviders.SELF_HOSTED, Hostname = "http://gpu-server:8080", ModelId = "second-model", Label = "Second" },
        Judge = new() { LLMProvider = LLMProviders.MISTRAL, ModelId = "judge-model", Label = "Judge" },
        Runs =
        [
            new()
            {
                Vote = ModelComparisonModelChoice.FIRST,
                JudgeChoice = ModelComparisonModelChoice.TIE,
                First = new() { Answered = true, FirstTokenTime = TimeSpan.FromMilliseconds(800), TotalTime = TimeSpan.FromSeconds(5) },
                Second = new() { Answered = true, FirstTokenTime = TimeSpan.FromSeconds(1), TotalTime = TimeSpan.FromSeconds(7) },
            },
            new()
            {
                First = new() { Answered = true, FirstTokenTime = TimeSpan.FromSeconds(1), TotalTime = TimeSpan.FromSeconds(4) },
                Second = new() { Answered = false },
            },
        ],
    };
}