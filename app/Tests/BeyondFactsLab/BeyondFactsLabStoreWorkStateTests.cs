using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the work states of the steps.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStoreWorkStateTests
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
    public async Task AStepWithoutAWorkStateGetsAnEmptyOne()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var workState = await store.LoadWorkStateAsync(created.ProjectId, BflStep.KNOWLEDGE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workState.Draft, Is.Null);
            Assert.That(workState.Confirmed, Is.Null);
            Assert.That(workState.FieldSuggestions, Is.Empty);
        }
    }

    [Test]
    public async Task AWorkStateIsStoredWithItsConfirmationAndSuggestions()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var confirmedAtUtc = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        var workState = new BflWorkState { FieldSuggestions = { [BflFieldCatalog.CORE_THESES] = "These" } };
        workState.Confirm(new Dictionary<string, string> { ["SUMMARY"] = "Stand" }, new Dictionary<BflStep, int> { [BflStep.FRAMEWORK] = 3 }, "fingerprint", confirmedAtUtc);
        workState.Draft = new(StringComparer.Ordinal) { ["SUMMARY"] = "Entwurf" };

        var changed = await store.SaveWorkStateAsync(created.ProjectId, BflStep.KNOWLEDGE, workState);
        var loaded = await this.CreateStore().LoadWorkStateAsync(created.ProjectId, BflStep.KNOWLEDGE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThanOrEqualTo(created.ModifiedAtUtc));
            Assert.That(loaded.Draft, Is.EqualTo(workState.Draft));
            Assert.That(loaded.LastRevision, Is.EqualTo(1));
            Assert.That(loaded.Confirmed!.Revision, Is.EqualTo(1));
            Assert.That(loaded.Confirmed.ConfirmedAtUtc, Is.EqualTo(confirmedAtUtc));
            Assert.That(loaded.Confirmed.Sections, Is.EqualTo(workState.Confirmed!.Sections));
            Assert.That(loaded.Confirmed.UpstreamRevisions, Is.EqualTo(workState.Confirmed.UpstreamRevisions));
            Assert.That(loaded.Confirmed.DetailsFingerprint, Is.EqualTo("fingerprint"));
            Assert.That(loaded.FieldSuggestions, Is.EqualTo(workState.FieldSuggestions));
        }
    }

    [Test]
    public async Task ADamagedWorkStateIsNeitherOpenedNorOverwritten()
    {
        const string DAMAGED = "{ \"draft\": ";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteWorkStateFileAsync(created.ProjectId, "02-knowledge", DAMAGED);

        var workStates = await store.LoadWorkStatesAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workStates.ContainsKey(BflStep.KNOWLEDGE), Is.False);
            Assert.That(workStates.ContainsKey(BflStep.FRAMEWORK), Is.True);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadWorkStateAsync(created.ProjectId, BflStep.KNOWLEDGE));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveWorkStateAsync(created.ProjectId, BflStep.KNOWLEDGE, new BflWorkState()));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(DAMAGED));
        }
    }

    [Test]
    public async Task AWorkStateOfANewerVersionIsNotOverwritten()
    {
        const string NEWER = """{ "schemaVersion": 2, "fieldSuggestions": {} }""";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var path = await this.WriteWorkStateFileAsync(created.ProjectId, "05-strategy", NEWER);

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveWorkStateAsync(created.ProjectId, BflStep.STRATEGY, new BflWorkState()));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(NEWER));
        }
    }

    [Test]
    public async Task AWorkStateOfAMissingPreparationIsNotWritten()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.SaveWorkStateAsync(Guid.NewGuid(), BflStep.FRAMEWORK, new BflWorkState()));
    }

    private async Task<string> WriteWorkStateFileAsync(Guid projectId, string stepFolder, string content)
    {
        var directory = Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME, projectId.ToString("D"), "steps", stepFolder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "work-state.json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}