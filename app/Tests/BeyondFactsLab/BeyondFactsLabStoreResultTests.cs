using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the results of a preparation.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStoreResultTests
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
    public async Task APreparationWithoutAResultGetsAnEmptyOne()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var result = await store.LoadResultAsync(created.ProjectId, BflResultKind.POSITION_PAPER);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Draft, Is.Null);
            Assert.That(result.Confirmed, Is.Null);
        }
    }

    [Test]
    public async Task AResultIsStoredWithItsDraftAndItsConfirmedVersion()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var confirmedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var result = new BflResult { Draft = new BflResultVersion { Text = "# Positionsblatt", WorkStateRevisions = { [BflStep.GOALS_AND_ROLE] = 3 } } };
        result.Confirm("# Positionsblatt\n\nBestätigt", confirmedAtUtc);
        result.Edit();
        result.Draft!.Text = "# Positionsblatt\n\nÜberarbeitet";

        var changed = await store.SaveResultAsync(created.ProjectId, BflResultKind.POSITION_PAPER, result);
        var loaded = await this.CreateStore().LoadResultAsync(created.ProjectId, BflResultKind.POSITION_PAPER);
        var others = await this.CreateStore().LoadResultAsync(created.ProjectId, BflResultKind.CONVERSATION_GUIDE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThanOrEqualTo(created.ModifiedAtUtc));
            Assert.That(loaded.Draft!.Text, Is.EqualTo("# Positionsblatt\n\nÜberarbeitet"));
            Assert.That(loaded.Confirmed!.Text, Is.EqualTo("# Positionsblatt\n\nBestätigt"));
            Assert.That(loaded.Confirmed.ChangedAtUtc, Is.EqualTo(confirmedAtUtc));
            Assert.That(loaded.Confirmed.WorkStateRevisions, Is.EqualTo(new Dictionary<BflStep, int> { [BflStep.GOALS_AND_ROLE] = 3 }));
            Assert.That(others.Confirmed, Is.Null, "Each kind of result has a file of its own.");
        }
    }

    [Test]
    public async Task ADamagedResultIsNeitherOpenedNorOverwritten()
    {
        const string DAMAGED = "{ \"draft\": ";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteResultFileAsync(created.ProjectId, "open-points.json", DAMAGED);

        var results = await store.LoadResultsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.ContainsKey(BflResultKind.OPEN_POINTS), Is.False);
            Assert.That(results.ContainsKey(BflResultKind.POSITION_PAPER), Is.True);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadResultAsync(created.ProjectId, BflResultKind.OPEN_POINTS));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveResultAsync(created.ProjectId, BflResultKind.OPEN_POINTS, new BflResult()));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(DAMAGED));
        }
    }

    [Test]
    public async Task AResultWithoutItsTextIsNotOpened()
    {
        const string WITHOUT_TEXT = """{ "schemaVersion": 1, "confirmed": { "text": null, "workStateRevisions": {} } }""";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await this.WriteResultFileAsync(created.ProjectId, "position-paper.json", WITHOUT_TEXT);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadResultAsync(created.ProjectId, BflResultKind.POSITION_PAPER));
    }

    [Test]
    public async Task AResultOfANewerVersionIsNotOverwritten()
    {
        const string NEWER = """{ "schemaVersion": 2 }""";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteResultFileAsync(created.ProjectId, "reflection-folder.json", NEWER);

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveResultAsync(created.ProjectId, BflResultKind.REFLECTION_FOLDER, new BflResult()));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(NEWER));
        }
    }

    [Test]
    public async Task AResultOfAMissingPreparationIsNotWritten()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.SaveResultAsync(Guid.NewGuid(), BflResultKind.POSITION_PAPER, new BflResult()));
    }

    private async Task<string> WriteResultFileAsync(Guid projectId, string fileName, string content)
    {
        var directory = Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, projectId.ToString("D"), "results");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}