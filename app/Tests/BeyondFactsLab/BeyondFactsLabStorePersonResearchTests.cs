using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the research on single people of a preparation.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStorePersonResearchTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

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
    public async Task APreparationWithoutResearchOnPeopleHasNone()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.LoadPersonResearchAsync(created.ProjectId, "Eva Klein"), Is.Null);
            Assert.That(await store.LoadAllPersonResearchAsync(created.ProjectId), Is.Empty);
        }
    }

    [Test]
    public async Task TheResearchOnAPersonIsStoredUnderTheIdOfTheName()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var research = new BflPersonResearch { Name = "Eva Klein" };
        research.Set(BflPersonResearchStage.PROFILE, new BflResearch
        {
            Text = "## Profil\n\nProfessorin",
            Details = [new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" }],
            SearchQueries = ["Eva Klein Klimapolitik"],
            Pages = [new() { Title = "Profil", Url = "https://example.org/klein" }],
            CreatedAtUtc = CREATED_AT_UTC,
        });

        var changed = await store.SavePersonResearchAsync(created.ProjectId, research);
        var loaded = await this.CreateStore().LoadPersonResearchAsync(created.ProjectId, "eva klein");
        var all = await this.CreateStore().LoadAllPersonResearchAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo("Eva Klein"));
            Assert.That(loaded.Stages.Single().Stage, Is.EqualTo(BflPersonResearchStage.PROFILE));
            Assert.That(loaded.StageOf(BflPersonResearchStage.PROFILE)!.Text, Is.EqualTo("## Profil\n\nProfessorin"));
            Assert.That(loaded.StageOf(BflPersonResearchStage.PROFILE)!.SearchQueries, Is.EqualTo(new[] { "Eva Klein Klimapolitik" }));
            Assert.That(loaded.StageOf(BflPersonResearchStage.PROFILE)!.CreatedAtUtc, Is.EqualTo(CREATED_AT_UTC));
            Assert.That(all.Single().Name, Is.EqualTo("Eva Klein"));
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThanOrEqualTo(created.ModifiedAtUtc));
            Assert.That(File.Exists(Path.Combine(this.PersonDirectory(created.ProjectId), $"{BflPersonId.Of("Eva Klein"):D}.json")), Is.True);
        }
    }

    [Test]
    public async Task AStoredStageIsWrittenAsStage()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var research = new BflPersonResearch { Name = "Eva Klein" };
        research.Set(BflPersonResearchStage.APPEARANCES, new BflResearch { Text = "Text", CreatedAtUtc = CREATED_AT_UTC });

        await store.SavePersonResearchAsync(created.ProjectId, research);
        var json = await File.ReadAllTextAsync(Path.Combine(this.PersonDirectory(created.ProjectId), $"{BflPersonId.Of("Eva Klein"):D}.json"));

        Assert.That(json, Does.Contain("\"stage\": \"APPEARANCES\""));
    }

    [Test]
    public async Task ADamagedResearchOnAPersonStaysAsItIs()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteFileAsync(created.ProjectId, $"{BflPersonId.Of("Eva Klein"):D}.json", "{ not json");

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadPersonResearchAsync(created.ProjectId, "Eva Klein"));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SavePersonResearchAsync(created.ProjectId, new BflPersonResearch { Name = "Eva Klein" }));
            Assert.That(await store.LoadAllPersonResearchAsync(created.ProjectId), Is.Empty);
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("{ not json"));
        }
    }

    [Test]
    public async Task AResearchOnAPersonOfANewerVersionStaysAsItIs()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await this.WriteFileAsync(created.ProjectId, $"{BflPersonId.Of("Eva Klein"):D}.json", $$"""{ "schemaVersion": {{BflPersonResearch.CURRENT_SCHEMA_VERSION + 1}}, "name": "Eva Klein", "stages": [] }""");

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadPersonResearchAsync(created.ProjectId, "Eva Klein"));
    }

    [Test]
    public async Task AResearchUnderTheNameOfAnotherPersonIsRefused()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await this.WriteFileAsync(created.ProjectId, $"{BflPersonId.Of("Eva Klein"):D}.json", """{ "schemaVersion": 1, "name": "Frank Ott", "stages": [] }""");

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadPersonResearchAsync(created.ProjectId, "Eva Klein"));
            Assert.That(await store.LoadAllPersonResearchAsync(created.ProjectId), Is.Empty);
        }
    }

    [Test]
    public async Task OtherFilesInTheFolderAreNoResearchOnPeople()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await this.WriteFileAsync(created.ProjectId, "notes.json", """{ "schemaVersion": 1, "name": "Eva Klein", "stages": [] }""");

        Assert.That(await store.LoadAllPersonResearchAsync(created.ProjectId), Is.Empty);
    }

    [Test]
    public async Task TheSelectionOfParticipantsIsStoredBesideTheResearch()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var selection = new BflParticipantSelection { Names = ["Eva Klein", "Frank Ott"], Source = BflParticipantSelectionSource.SUGGESTION, ListCreatedAtUtc = CREATED_AT_UTC };

        await store.SaveParticipantSelectionAsync(created.ProjectId, selection);
        var loaded = await this.CreateStore().LoadParticipantSelectionAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Names, Is.EqualTo(new[] { "Eva Klein", "Frank Ott" }));
            Assert.That(loaded.Source, Is.EqualTo(BflParticipantSelectionSource.SUGGESTION));
            Assert.That(loaded.ListCreatedAtUtc, Is.EqualTo(CREATED_AT_UTC));
            Assert.That(await store.LoadAllPersonResearchAsync(created.ProjectId), Is.Empty);
            Assert.That(File.Exists(Path.Combine(this.PersonDirectory(created.ProjectId), "..", "participant-selection.json")), Is.True);
        }
    }

    [Test]
    public async Task APreparationWithoutASelectionOfParticipantsHasNone()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        Assert.That(await store.LoadParticipantSelectionAsync(created.ProjectId), Is.Null);
    }

    [Test]
    public async Task ADamagedSelectionOfParticipantsStaysAsItIs()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteFileAsync(created.ProjectId, Path.Combine("..", "participant-selection.json"), """{ "schemaVersion": 1, "names": [ null ] }""");

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadParticipantSelectionAsync(created.ProjectId));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveParticipantSelectionAsync(created.ProjectId, new BflParticipantSelection()));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("""{ "schemaVersion": 1, "names": [ null ] }"""));
        }
    }

    [Test]
    public async Task ResearchOnAPersonOfAMissingPreparationIsNotStored()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.SavePersonResearchAsync(Guid.NewGuid(), new BflPersonResearch { Name = "Eva Klein" }));
    }

    private async Task<string> WriteFileAsync(Guid projectId, string fileName, string content)
    {
        var directory = this.PersonDirectory(projectId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private string PersonDirectory(Guid projectId) => Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, projectId.ToString("D"), "steps", "strategy", "research", "participants");

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}