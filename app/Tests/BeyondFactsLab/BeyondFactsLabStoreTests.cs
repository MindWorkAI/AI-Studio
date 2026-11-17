using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores its preparations on disk.
/// </summary>
/// <remarks>
/// People maintain the list of their preparations themselves: nothing gets deleted on its own, also not a
/// preparation which this version cannot open.
/// </remarks>
[TestFixture]
public sealed class BeyondFactsLabStoreTests
{
    private string dataDirectory = string.Empty;

    private string RootDirectory => Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME);

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
    public async Task ACreatedPreparationIsListedAndLoaded()
    {
        var store = this.CreateStore();

        var created = await store.CreateAsync("  Panel on climate adaptation  ");
        var projects = await store.ListProjectsAsync();
        var loaded = await store.LoadAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Name, Is.EqualTo("Panel on climate adaptation"));
            Assert.That(created.SchemaVersion, Is.EqualTo(BflProject.CURRENT_SCHEMA_VERSION));
            Assert.That(created.HappenedAtUtc, Is.Null);
            Assert.That(projects, Has.Count.EqualTo(1));
            Assert.That(projects[0].IsAvailable, Is.True);
            Assert.That(projects[0].Name, Is.EqualTo(created.Name));
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo(created.Name));
            Assert.That(loaded.CreatedAtUtc, Is.EqualTo(created.CreatedAtUtc));
            Assert.That(File.Exists(this.ProjectPath(created.ProjectId)), Is.True);
        }
    }

    [Test]
    public async Task ANewStoreFindsThePreparationsOfTheLastOne()
    {
        var created = await this.CreateStore().CreateAsync("Interview");

        var projects = await this.CreateStore().ListProjectsAsync();

        Assert.That(projects.Select(project => project.ProjectId), Is.EqualTo(new[] { created.ProjectId }));
    }

    [Test]
    public async Task ThePreparationChangedLastComesFirst()
    {
        var store = this.CreateStore();
        var first = await store.CreateAsync("First");
        var second = await store.CreateAsync("Second");

        await store.RenameAsync(first.ProjectId, "First, renamed");
        var projects = await store.ListProjectsAsync();

        Assert.That(projects.Select(project => project.ProjectId), Is.EqualTo(new[] { first.ProjectId, second.ProjectId }));
    }

    [Test]
    public async Task RenamingKeepsEverythingElse()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Old name");

        var renamed = await store.RenameAsync(created.ProjectId, "New name");
        var loaded = await store.LoadAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(renamed.Name, Is.EqualTo("New name"));
            Assert.That(loaded!.Name, Is.EqualTo("New name"));
            Assert.That(loaded.CreatedAtUtc, Is.EqualTo(created.CreatedAtUtc));
            Assert.That(loaded.ModifiedAtUtc, Is.GreaterThanOrEqualTo(created.ModifiedAtUtc));
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\a\t")]
    public async Task AnEmptyNameIsRefused(string name)
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.CreateAsync(name));
    }

    [Test]
    public async Task ANameLosesControlCharactersAndGetsCut()
    {
        var store = this.CreateStore();

        var created = await store.CreateAsync("Talk\0 show" + new string('x', 300));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Name, Does.StartWith("Talk show"));
            Assert.That(created.Name, Has.Length.EqualTo(BeyondFactsLabStore.MAX_NAME_LENGTH));
        }
    }

    [Test]
    public async Task TheEventCanBeMarkedAsHavingTakenPlaceAndBack()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var happenedAt = new DateTimeOffset(2026, 12, 10, 14, 0, 0, TimeSpan.FromHours(1));

        await store.SetHappenedAsync(created.ProjectId, happenedAt);
        var afterMarking = await store.LoadAsync(created.ProjectId);
        await store.SetHappenedAsync(created.ProjectId, null);
        var afterTakingBack = await store.LoadAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterMarking!.HappenedAtUtc, Is.EqualTo(happenedAt));
            Assert.That(afterMarking.HappenedAtUtc!.Value.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(afterTakingBack!.HappenedAtUtc, Is.Null);
        }
    }

    [Test]
    public async Task DeletingRemovesTheFolderAndTheSelection()
    {
        var store = this.CreateStore();
        var kept = await store.CreateAsync("Kept");
        var deleted = await store.CreateAsync("Deleted");
        await store.RememberSelectionAsync(deleted.ProjectId);

        await store.DeleteAsync(deleted.ProjectId);
        var projects = await store.ListProjectsAsync();
        var newStore = this.CreateStore();
        await newStore.ListProjectsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projects.Select(project => project.ProjectId), Is.EqualTo(new[] { kept.ProjectId }));
            Assert.That(Directory.Exists(Path.Combine(this.RootDirectory, deleted.ProjectId.ToString("D"))), Is.False);
            Assert.That(store.LastSelectedProjectId, Is.Null);
            Assert.That(newStore.LastSelectedProjectId, Is.Null);
        }
    }

    [Test]
    public async Task TheSelectionSurvivesANewStore()
    {
        var store = this.CreateStore();
        await store.CreateAsync("Other");
        var selected = await store.CreateAsync("Selected");

        await store.RememberSelectionAsync(selected.ProjectId);
        var newStore = this.CreateStore();
        await newStore.ListProjectsAsync();

        Assert.That(newStore.LastSelectedProjectId, Is.EqualTo(selected.ProjectId));
    }

    [Test]
    public async Task ASelectionOfAMissingPreparationIsDropped()
    {
        Directory.CreateDirectory(this.RootDirectory);
        await File.WriteAllTextAsync(Path.Combine(this.RootDirectory, "selection.json"), $"\"{Guid.NewGuid():D}\"");

        var store = this.CreateStore();
        await store.ListProjectsAsync();

        Assert.That(store.LastSelectedProjectId, Is.Null);
    }

    [Test]
    public async Task NoTemporaryFileStaysBehind()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await store.RenameAsync(created.ProjectId, "Panel, renamed");
        await store.SetHappenedAsync(created.ProjectId, DateTimeOffset.UtcNow);
        await store.RememberSelectionAsync(created.ProjectId);

        Assert.That(Directory.EnumerateFiles(this.RootDirectory, "*.tmp-*", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public async Task ANewStoreRemovesTemporaryFilesACrashLeftBehind()
    {
        var created = await this.CreateStore().CreateAsync("Panel");
        var leftOver = $"{this.ProjectPath(created.ProjectId)}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(leftOver, "{");

        await this.CreateStore().ListProjectsAsync();

        Assert.That(File.Exists(leftOver), Is.False);
    }

    [Test]
    public async Task ADamagedPreparationIsListedButNeitherOpenedNorChangedNorDeleted()
    {
        const string DAMAGED = "{ \"name\": \"Damaged panel\", \"projectId\": ";
        var projectId = Guid.NewGuid();
        var path = await this.WriteProjectFileAsync(projectId, DAMAGED);
        var store = this.CreateStore();

        var projects = await store.ListProjectsAsync();
        var loaded = await store.LoadAsync(projectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projects, Has.Count.EqualTo(1));
            Assert.That(projects[0].ProjectId, Is.EqualTo(projectId));
            Assert.That(projects[0].Status, Is.EqualTo(BflProjectLoadStatus.UNAVAILABLE));
            Assert.That(projects[0].IsAvailable, Is.False);
            Assert.That(loaded, Is.Null);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.RenameAsync(projectId, "Repaired"));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(DAMAGED));
        }
    }

    [Test]
    public async Task APreparationOfANewerVersionIsListedWithItsNameAndNotWritten()
    {
        var projectId = Guid.NewGuid();
        var json = $$"""
                     {
                       "schemaVersion": {{BflProject.CURRENT_SCHEMA_VERSION + 1}},
                       "projectId": "{{projectId:D}}",
                       "name": "Panel of the future",
                       "createdAtUtc": "2026-11-01T10:00:00+00:00",
                       "modifiedAtUtc": "2026-11-02T10:00:00+00:00",
                       "fieldOfANewerVersion": true
                     }
                     """;

        var path = await this.WriteProjectFileAsync(projectId, json);
        var store = this.CreateStore();

        var projects = await store.ListProjectsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projects[0].Status, Is.EqualTo(BflProjectLoadStatus.NEWER_VERSION));
            Assert.That(projects[0].Name, Is.EqualTo("Panel of the future"));
            Assert.That(projects[0].ModifiedAtUtc, Is.EqualTo(new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero)));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SetHappenedAsync(projectId, DateTimeOffset.UtcNow));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo(json));
        }
    }

    [Test]
    public async Task APreparationWhoseFileNamesAnotherIdIsNotOpened()
    {
        var projectId = Guid.NewGuid();
        await this.WriteProjectFileAsync(projectId, $$"""{ "schemaVersion": 1, "projectId": "{{Guid.NewGuid():D}}", "name": "Moved" }""");
        var store = this.CreateStore();

        var projects = await store.ListProjectsAsync();
        var loaded = await store.LoadAsync(projectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(projects[0].Status, Is.EqualTo(BflProjectLoadStatus.UNAVAILABLE));
            Assert.That(projects[0].Name, Is.EqualTo("Moved"));
            Assert.That(loaded, Is.Null);
        }
    }

    [Test]
    public async Task APreparationWhichCannotBeOpenedCanStillBeDeleted()
    {
        var projectId = Guid.NewGuid();
        await this.WriteProjectFileAsync(projectId, "not JSON at all");
        var store = this.CreateStore();

        await store.DeleteAsync(projectId);

        Assert.That(await store.ListProjectsAsync(), Is.Empty);
    }

    [Test]
    public async Task FoldersWhichAreNoPreparationsAreIgnored()
    {
        Directory.CreateDirectory(Path.Combine(this.RootDirectory, "promptOverrides"));
        var created = await this.CreateStore().CreateAsync("Panel");

        var projects = await this.CreateStore().ListProjectsAsync();

        Assert.That(projects.Select(project => project.ProjectId), Is.EqualTo(new[] { created.ProjectId }));
    }

    [Test]
    public async Task UmlautsStayReadableInTheFile()
    {
        var created = await this.CreateStore().CreateAsync("Podiumsdiskussion über Gewässerschutz");

        var json = await File.ReadAllTextAsync(this.ProjectPath(created.ProjectId));

        Assert.That(json, Does.Contain("Podiumsdiskussion über Gewässerschutz"));
    }

    [Test]
    public async Task AFormIsStoredInTheFolderOfItsStepAndCountsAsAChange()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.FORMAT, "Panel discussion");

        var changed = await store.SaveFormAsync(created.ProjectId, BflStep.FRAMEWORK, form);
        var loaded = await this.CreateStore().LoadFormAsync(created.ProjectId, BflStep.FRAMEWORK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"), "steps", "01-framework", "form.json")), Is.True);
            Assert.That(loaded.Get(BflFieldCatalog.FORMAT), Is.EqualTo("Panel discussion"));
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThan(created.ModifiedAtUtc));
            Assert.That((await store.LoadAsync(created.ProjectId))!.ModifiedAtUtc, Is.EqualTo(changed.ModifiedAtUtc));
        }
    }

    [Test]
    public async Task AStepWithoutAFormGetsAnEmptyOne()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var form = await store.LoadFormAsync(created.ProjectId, BflStep.STRATEGY);
        var forms = await store.LoadFormsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(form.Values, Is.Empty);
            Assert.That(forms.Keys, Is.EquivalentTo(BflStep.All));
        }
    }

    [Test]
    public async Task AValueOfAFieldThisVersionDoesNotKnowIsKept()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var formPath = await this.WriteFormFileAsync(created.ProjectId, "02-knowledge", """{ "schemaVersion": 1, "values": { "field_of_a_newer_version": "kept" } }""");

        var form = await store.LoadFormAsync(created.ProjectId, BflStep.KNOWLEDGE);
        form.Set(BflFieldCatalog.CORE_THESES, "Thesis");
        await store.SaveFormAsync(created.ProjectId, BflStep.KNOWLEDGE, form);

        var json = await File.ReadAllTextAsync(formPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Does.Contain("field_of_a_newer_version"));
            Assert.That(json, Does.Contain("Thesis"));
        }
    }

    [Test]
    public async Task ADamagedFormIsNeitherOpenedNorOverwritten()
    {
        const string DAMAGED = "{ \"values\": ";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var formPath = await this.WriteFormFileAsync(created.ProjectId, "01-framework", DAMAGED);

        var forms = await store.LoadFormsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forms.ContainsKey(BflStep.FRAMEWORK), Is.False);
            Assert.That(forms.ContainsKey(BflStep.KNOWLEDGE), Is.True);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadFormAsync(created.ProjectId, BflStep.FRAMEWORK));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveFormAsync(created.ProjectId, BflStep.FRAMEWORK, new BflStepFormData()));
            Assert.That(await File.ReadAllTextAsync(formPath), Is.EqualTo(DAMAGED));
        }
    }

    [Test]
    public async Task AFormOfANewerVersionIsNotOverwritten()
    {
        const string NEWER = """{ "schemaVersion": 2, "values": {} }""";
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var formPath = await this.WriteFormFileAsync(created.ProjectId, "05-strategy", NEWER);

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveFormAsync(created.ProjectId, BflStep.STRATEGY, new BflStepFormData()));
            Assert.That(await File.ReadAllTextAsync(formPath), Is.EqualTo(NEWER));
        }
    }

    [Test]
    public async Task AFormOfAPreparationWhichCannotBeOpenedIsNotWritten()
    {
        var projectId = Guid.NewGuid();
        await this.WriteProjectFileAsync(projectId, "not JSON at all");
        var store = this.CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveFormAsync(projectId, BflStep.FRAMEWORK, new BflStepFormData()));

        Assert.That(Directory.Exists(Path.Combine(this.RootDirectory, projectId.ToString("D"), "steps")), Is.False);
    }

    [Test]
    public async Task TheStepShownLastIsRememberedWithoutCountingAsAChange()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        await store.SetCurrentStepAsync(created.ProjectId, BflStep.STRATEGY);
        var loaded = await this.CreateStore().LoadAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.CurrentStep, Is.EqualTo(BflStep.FRAMEWORK));
            Assert.That(loaded!.CurrentStep, Is.EqualTo(BflStep.STRATEGY));
            Assert.That(loaded.ModifiedAtUtc, Is.EqualTo(created.ModifiedAtUtc));
        }
    }

    [Test]
    public async Task TheProviderIsRememberedWithoutCountingAsAChange()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        await store.SetProviderAsync(created.ProjectId, "provider-1");
        var loaded = await this.CreateStore().LoadAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.ProviderId, Is.Empty, "A new preparation starts with the preselected provider.");
            Assert.That(loaded!.ProviderId, Is.EqualTo("provider-1"));
            Assert.That(loaded.ModifiedAtUtc, Is.EqualTo(created.ModifiedAtUtc));
        }
    }

    [Test]
    public async Task APreparationWithoutAProviderIdIsNotOpened()
    {
        var projectId = Guid.NewGuid();
        await this.WriteProjectFileAsync(projectId, $$"""{ "schemaVersion": 1, "projectId": "{{projectId:D}}", "name": "Panel", "providerId": null }""");

        var loaded = await this.CreateStore().LoadAsync(projectId);

        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task APreparationWithAnUnknownStepIsNotOpened()
    {
        var projectId = Guid.NewGuid();
        await this.WriteProjectFileAsync(projectId, $$"""{ "schemaVersion": 1, "projectId": "{{projectId:D}}", "name": "Panel", "currentStep": 42 }""");

        var loaded = await this.CreateStore().LoadAsync(projectId);

        Assert.That(loaded, Is.Null);
    }

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });

    private async Task<string> WriteFormFileAsync(Guid projectId, string stepFolder, string content)
    {
        var path = Path.Combine(this.RootDirectory, projectId.ToString("D"), "steps", stepFolder, "form.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private string ProjectPath(Guid projectId) => Path.Combine(this.RootDirectory, projectId.ToString("D"), "project.json");

    private async Task<string> WriteProjectFileAsync(Guid projectId, string content)
    {
        var path = this.ProjectPath(projectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}