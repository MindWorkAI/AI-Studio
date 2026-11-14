using AIStudio.Assistants.ModelComparison;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the Model Comparison assistant stores its presets as files below the data directory.
/// </summary>
[TestFixture]
public sealed class ModelComparisonStorePresetTests
{
    private string dataDirectory = string.Empty;

    private string PresetsDirectory => Path.Combine(this.dataDirectory, ModelComparisonStore.ROOT_DIRECTORY_NAME, "presets");

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
    public async Task ASavedPresetIsListedWithAllItsFields()
    {
        var store = this.CreateStore();
        var preset = Preset("  Summaries  ");

        await store.SavePresetAsync(preset);
        var presets = await store.ListPresetsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presets, Has.Count.EqualTo(1));
            Assert.That(presets[0], Is.EqualTo(preset with { Name = "Summaries" }), "The name is trimmed, everything else stays as it was.");
            Assert.That(File.Exists(Path.Combine(this.PresetsDirectory, $"{preset.Id:D}.json")), Is.True);
        }
    }

    [Test]
    public async Task ANewStoreFindsThePresetsOfTheLastOne()
    {
        var preset = Preset("Summaries");
        await this.CreateStore().SavePresetAsync(preset);

        var presets = await this.CreateStore().ListPresetsAsync();

        Assert.That(presets.Select(listed => listed.Id), Is.EqualTo(new[] { preset.Id }));
    }

    [Test]
    public async Task SavingAgainReplacesThePreset()
    {
        var store = this.CreateStore();
        var preset = Preset("Summaries");
        await store.SavePresetAsync(preset);

        await store.SavePresetAsync(preset with { Name = "Translations", RunCount = 10 });
        var presets = await store.ListPresetsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presets, Has.Count.EqualTo(1));
            Assert.That(presets[0].Name, Is.EqualTo("Translations"));
            Assert.That(presets[0].RunCount, Is.EqualTo(10));
        }
    }

    [Test]
    public async Task ThePresetsAreSortedByName()
    {
        var store = this.CreateStore();
        await store.SavePresetAsync(Preset("Translations"));
        await store.SavePresetAsync(Preset("abstracts"));
        await store.SavePresetAsync(Preset("Summaries"));

        var presets = await store.ListPresetsAsync();

        Assert.That(presets.Select(preset => preset.Name), Is.EqualTo(new[] { "abstracts", "Summaries", "Translations" }));
    }

    [Test]
    public async Task ADeletedPresetIsGone()
    {
        var store = this.CreateStore();
        var kept = Preset("Kept");
        var deleted = Preset("Deleted");
        await store.SavePresetAsync(kept);
        await store.SavePresetAsync(deleted);

        await store.DeletePresetAsync(deleted.Id);
        var presets = await store.ListPresetsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presets.Select(preset => preset.Id), Is.EqualTo(new[] { kept.Id }));
            Assert.That(File.Exists(Path.Combine(this.PresetsDirectory, $"{deleted.Id:D}.json")), Is.False);
        }
    }

    [Test]
    public async Task APresetThisVersionCannotReadIsLeftOutButKept()
    {
        var store = this.CreateStore();
        var readable = Preset("Readable");
        await store.SavePresetAsync(readable);

        // A newer version may write a field this one does not know:
        var unreadablePath = Path.Combine(this.PresetsDirectory, $"{Guid.NewGuid():D}.json");
        await File.WriteAllTextAsync(unreadablePath, """{ "name": "From a newer version", "fieldOfANewerVersion": true }""");

        var presets = await store.ListPresetsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presets.Select(preset => preset.Id), Is.EqualTo(new[] { readable.Id }));
            Assert.That(File.Exists(unreadablePath), Is.True);
        }
    }

    [Test]
    public async Task TheFileNameDecidesTheIdOfAPreset()
    {
        var store = this.CreateStore();
        var preset = Preset("Summaries");
        await store.SavePresetAsync(preset);

        // A copied file keeps the ID of its original inside; its own name is what a save or delete has to reach:
        var copyId = Guid.NewGuid();
        File.Copy(Path.Combine(this.PresetsDirectory, $"{preset.Id:D}.json"), Path.Combine(this.PresetsDirectory, $"{copyId:D}.json"));

        var presets = await store.ListPresetsAsync();

        Assert.That(presets.Select(listed => listed.Id), Is.EquivalentTo(new[] { preset.Id, copyId }));
    }

    [Test]
    public async Task APresetWithoutANameIsRefused()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SavePresetAsync(Preset("   ")));
    }

    private ModelComparisonStore CreateStore() => new(NullLogger<ModelComparisonStore>.Instance, new ModelComparisonStorageOptions { DataDirectory = this.dataDirectory });

    private static ModelComparisonPreset Preset(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        FirstProviderId = "first",
        SecondProviderId = "second",
        JudgeEnabled = true,
        JudgeProviderId = "judge",
        JudgeInstructions = "Keep every number.",
        Context = "The document, with umlauts: Größe.",
        Question = "Summarize it.",
        RunCount = 5,
    };
}