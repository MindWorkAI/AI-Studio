using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the research of a preparation.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStoreResearchTests
{
    private string dataDirectory = string.Empty;

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task APreparationWithoutResearchHasNone()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.LoadResearchAsync(created.ProjectId, BflResearchTopic.EVENT), Is.Null);
            Assert.That(await store.LoadAllResearchAsync(created.ProjectId), Is.Empty);
        }
    }

    [Test]
    public async Task AResearchIsStoredForItsTopic()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var research = new BflResearch
        {
            Text = "## Teilnehmende\n\n### Person A",
            Details = [new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" }],
            SearchQueries = ["Podium Klimapolitik"],
            Pages = [new() { Title = "Programm", Url = "https://example.org/programm" }],
            CreatedAtUtc = new DateTimeOffset(2026, 11, 10, 14, 0, 0, TimeSpan.Zero),
        };

        var changed = await store.SaveResearchAsync(created.ProjectId, BflResearchTopic.PARTICIPANTS, research);
        var loaded = await this.CreateStore().LoadResearchAsync(created.ProjectId, BflResearchTopic.PARTICIPANTS);
        var all = await this.CreateStore().LoadAllResearchAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Text, Is.EqualTo(research.Text));
            Assert.That(loaded.Details.Single().Value, Is.EqualTo("Klimapolitik"));
            Assert.That(loaded.SearchQueries, Is.EqualTo(new[] { "Podium Klimapolitik" }));
            Assert.That(loaded.Pages.Single().Url, Is.EqualTo("https://example.org/programm"));
            Assert.That(loaded.CreatedAtUtc, Is.EqualTo(research.CreatedAtUtc));
            Assert.That(all.Keys, Is.EquivalentTo(new[] { BflResearchTopic.PARTICIPANTS }));
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThanOrEqualTo(created.ModifiedAtUtc));
            Assert.That(File.Exists(Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, created.ProjectId.ToString("D"), "steps", "05-strategy", "research", "participants.json")), Is.True);
        }
    }

    [Test]
    public async Task ADamagedResearchStaysAsItIs()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteResearchFileAsync(created.ProjectId, "event.json", "{ not json");

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadResearchAsync(created.ProjectId, BflResearchTopic.EVENT));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveResearchAsync(created.ProjectId, BflResearchTopic.EVENT, new BflResearch { Text = "Neu" }));
            Assert.That(await store.LoadAllResearchAsync(created.ProjectId), Is.Empty);
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("{ not json"));
        }
    }

    [Test]
    public async Task AResearchOfANewerVersionStaysAsItIs()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await this.WriteResearchFileAsync(created.ProjectId, "debate.json", $$"""{ "schemaVersion": {{BflResearch.CURRENT_SCHEMA_VERSION + 1}}, "text": "Neu" }""");

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadResearchAsync(created.ProjectId, BflResearchTopic.DEBATE));
    }

    [Test]
    public async Task ResearchForAMissingPreparationIsNotStored()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.SaveResearchAsync(Guid.NewGuid(), BflResearchTopic.EVENT, new BflResearch { Text = "Text" }));
    }

    private async Task<string> WriteResearchFileAsync(Guid projectId, string fileName, string content)
    {
        var directory = Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, projectId.ToString("D"), "steps", "05-strategy", "research");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}