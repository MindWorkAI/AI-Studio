using AIStudio.Assistants.BeyondFactsLab;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the materials of a preparation.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStoreMaterialTests
{
    private const string HASH = "0f343b0931126a20f133d67c2b018a3b5f1d1f5c1f6a8e0a3d5a5a3e9c6a1b2c";

    private const string OTHER_HASH = "1f343b0931126a20f133d67c2b018a3b5f1d1f5c1f6a8e0a3d5a5a3e9c6a1b2c";

    private string dataDirectory = string.Empty;

    private string RootDirectory => Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME);

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-materials-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task AMaterialIsStoredWithItsText()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Einladung.docx", HASH), "Podium am 10. Dezember");
        var changed = await store.LoadAsync(created.ProjectId);
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);
        var text = await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, 1_000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(addition.IsDuplicate, Is.False);
            Assert.That(materials, Has.Count.EqualTo(1));
            Assert.That(materials[0].Material!.Name, Is.EqualTo("Einladung.docx"));
            Assert.That(materials[0].Material!.TextLength, Is.EqualTo("Podium am 10. Dezember".Length));
            Assert.That(materials[0].Material!.SourceBytes, Is.EqualTo(2_048));
            Assert.That(text, Is.EqualTo("Podium am 10. Dezember"));
            Assert.That(changed!.ModifiedAtUtc, Is.GreaterThan(created.ModifiedAtUtc), "A material is work on the preparation.");
            Assert.That(File.Exists(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json")), Is.True);
            Assert.That(File.Exists(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".md")), Is.True);
        }
    }

    [Test]
    public async Task AnImageIsStoredWithItsCopy()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        byte[] copy = [0x89, 0x50, 0x4E, 0x47];

        var addition = await store.AddImageMaterialAsync(created.ProjectId, ImageMaterial("Poster.tiff", HASH), copy);
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);
        var path = store.MaterialImagePath(created.ProjectId, materials[0].Material!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(addition.IsDuplicate, Is.False);
            Assert.That(materials[0].Material!.Kind, Is.EqualTo(BflMaterialKind.IMAGE));
            Assert.That(materials[0].Material!.Image!.Width, Is.EqualTo(2_560));
            Assert.That(materials[0].Material!.TextLength, Is.Zero);
            Assert.That(path, Is.EqualTo(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".png")));
            Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(copy));
            Assert.That(File.Exists(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".md")), Is.False, "An image has no text.");
        }
    }

    [Test]
    public async Task AnImageWithoutItsCopyCannotBeOpened()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddImageMaterialAsync(created.ProjectId, ImageMaterial("Poster.png", HASH), new byte[] { 1 });

        File.Delete(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".png"));
        var materials = await store.ListMaterialsAsync(created.ProjectId);

        Assert.That(materials.Single().IsAvailable, Is.False);
    }

    [Test]
    public async Task DeletingAnImageDeletesItsCopy()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddImageMaterialAsync(created.ProjectId, ImageMaterial("Poster.png", HASH), new byte[] { 1 });

        await store.DeleteMaterialAsync(created.ProjectId, addition.Material.MaterialId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.ListMaterialsAsync(created.ProjectId), Is.Empty);
            Assert.That(File.Exists(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".png")), Is.False);
        }
    }

    [Test]
    public async Task ARecordingIsStoredWithItsTranscript()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var material = Material("Interview.mp3", HASH);
        material.Kind = BflMaterialKind.RECORDING;

        var addition = await store.AddMaterialAsync(created.ProjectId, material, "Guten Tag.");
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(materials[0].Material!.Kind, Is.EqualTo(BflMaterialKind.RECORDING));
            Assert.That(await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, 100), Is.EqualTo("Guten Tag."));
        }
    }

    [Test]
    public async Task AMaterialStoredBeforeTheKindsIsADocument()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        var json = this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json");

        var lines = (await File.ReadAllLinesAsync(json)).Where(line => !line.Contains("\"kind\"", StringComparison.Ordinal) && !line.Contains("\"image\"", StringComparison.Ordinal));
        await File.WriteAllLinesAsync(json, lines);
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);

        Assert.That(materials.Single().Material!.Kind, Is.EqualTo(BflMaterialKind.DOCUMENT));
    }

    [Test]
    public async Task AnImageWithoutTheDescriptionOfItsCopyIsDamaged()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddImageMaterialAsync(created.ProjectId, ImageMaterial("Poster.png", HASH), new byte[] { 1 });
        var json = this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json");

        await File.WriteAllTextAsync(json, (await File.ReadAllTextAsync(json)).Replace("\"png\"", "\"../../x\"", StringComparison.Ordinal));
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);

        Assert.That(materials.Single().IsAvailable, Is.False, "An unknown extension could point anywhere.");
    }

    [Test]
    public async Task AnImageCannotBeAddedAsText()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AddMaterialAsync(Guid.NewGuid(), ImageMaterial("Poster.png", HASH), "Text"));
    }

    [Test]
    public async Task ADocumentCannotBeAddedAsAnImage()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AddImageMaterialAsync(Guid.NewGuid(), Material("Paper.pdf", HASH), new byte[] { 1 }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AMaterialKeepsWhetherItsTextIsOnlyTheBeginning(bool isShortened)
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var material = Material("Buch.pdf", HASH);
        material.IsShortened = isShortened;

        await store.AddMaterialAsync(created.ProjectId, material, "Kapitel 1");
        var materials = await this.CreateStore().ListMaterialsAsync(created.ProjectId);

        Assert.That(materials[0].Material!.IsShortened, Is.EqualTo(isShortened));
    }

    [TestCase("/Users/person/Documents/Talk.pdf")]
    [TestCase(@"C:\Users\person\Documents\Talk.pdf")]
    [TestCase("Talk.pdf")]
    public async Task TheStoreKeepsNoPath(string name)
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var addition = await store.AddMaterialAsync(created.ProjectId, Material(name, HASH), "Text");
        var json = await File.ReadAllTextAsync(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(addition.Material.Name, Is.EqualTo("Talk.pdf"));
            Assert.That(json, Does.Not.Contain("Users"));
            Assert.That(json, Does.Not.Contain("Documents"));
        }
    }

    [Test]
    public async Task AFolderAloneIsNoName()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AddMaterialAsync(created.ProjectId, Material("/Users/person/", HASH), "Text"));
    }

    [Test]
    public async Task ADocumentWithTheSameContentIsStoredOnce()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var first = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        var second = await store.AddMaterialAsync(created.ProjectId, Material("Paper (Kopie).pdf", HASH.ToUpperInvariant()), "Text");
        var materials = await store.ListMaterialsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.IsDuplicate, Is.True);
            Assert.That(second.Material.MaterialId, Is.EqualTo(first.Material.MaterialId));
            Assert.That(second.Material.Name, Is.EqualTo("Paper.pdf"));
            Assert.That(materials, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task MaterialsAreListedInTheOrderTheyWereAdded()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        await store.AddMaterialAsync(created.ProjectId, Material("Erstes.pdf", HASH), "Eins");
        await Task.Delay(20);
        await store.AddMaterialAsync(created.ProjectId, Material("Zweites.pdf", OTHER_HASH), "Zwei");
        var materials = await store.ListMaterialsAsync(created.ProjectId);

        Assert.That(materials.Select(entry => entry.Material!.Name), Is.EqualTo(new[] { "Erstes.pdf", "Zweites.pdf" }));
    }

    [Test]
    public async Task APreparationWithoutMaterialsHasNone()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        Assert.That(await store.ListMaterialsAsync(created.ProjectId), Is.Empty);
    }

    [Test]
    public async Task AMaterialWithoutTextCannotBeOpened()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");

        File.Delete(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".md"));
        var materials = await store.ListMaterialsAsync(created.ProjectId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(materials, Has.Count.EqualTo(1));
            Assert.That(materials[0].IsAvailable, Is.False);
            Assert.That(materials[0].MaterialId, Is.EqualTo(addition.Material.MaterialId));
        }
    }

    [Test]
    public async Task ADamagedMaterialIsListedAndCanBeDeleted()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        var json = this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json");

        await File.WriteAllTextAsync(json, """{ "schemaVersion": 1, "fieldOfANewerVersion": true }""");
        var damaged = await store.ListMaterialsAsync(created.ProjectId);
        await store.DeleteMaterialAsync(created.ProjectId, addition.Material.MaterialId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(damaged.Single().IsAvailable, Is.False);
            Assert.That(await store.ListMaterialsAsync(created.ProjectId), Is.Empty);
            Assert.That(File.Exists(json), Is.False);
            Assert.That(File.Exists(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".md")), Is.False);
        }
    }

    [Test]
    public async Task ADamagedMaterialIsNoDuplicate()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        await File.WriteAllTextAsync(this.MaterialFile(created.ProjectId, addition.Material.MaterialId, ".json"), "{");

        var again = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");

        Assert.That(again.IsDuplicate, Is.False, "The person cannot see the damaged one, so the document has to come back.");
    }

    [Test]
    public async Task DeletingAMaterialCountsAsAChange()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        var afterAdding = await store.LoadAsync(created.ProjectId);
        await Task.Delay(20);

        var afterDeleting = await store.DeleteMaterialAsync(created.ProjectId, addition.Material.MaterialId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterDeleting.ModifiedAtUtc, Is.GreaterThan(afterAdding!.ModifiedAtUtc));
            Assert.That(await store.ListMaterialsAsync(created.ProjectId), Is.Empty);
        }
    }

    [Test]
    public async Task ReadingStopsAtTheLimit()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var text = string.Concat(Enumerable.Repeat("Ein Satz über Wissenschaft. ", 10_000));
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), text);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, 100_000), Is.EqualTo(text[..100_000]));
            Assert.That(await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, text.Length + 10), Is.EqualTo(text));
            Assert.That(await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, 0), Is.Empty);
        }
    }

    [Test]
    public async Task ReadingADeletedMaterialFails()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var addition = await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");
        await store.DeleteMaterialAsync(created.ProjectId, addition.Material.MaterialId);

        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.ReadMaterialTextAsync(created.ProjectId, addition.Material.MaterialId, 100));
    }

    [Test]
    public async Task AMaterialOfAMissingPreparationIsNotStored()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.AddMaterialAsync(Guid.NewGuid(), Material("Paper.pdf", HASH), "Text"));
    }

    [TestCase("")]
    [TestCase("  \n")]
    public async Task AMaterialWithoutTextIsRefused(string text)
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), text));
    }

    [Test]
    public async Task DeletingAPreparationDeletesItsMaterials()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        await store.AddMaterialAsync(created.ProjectId, Material("Paper.pdf", HASH), "Text");

        await store.DeleteAsync(created.ProjectId);

        Assert.That(Directory.Exists(Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"))), Is.False);
    }

    private static BflMaterial Material(string name, string hash) => new()
    {
        Name = name,
        SourceHash = hash,
        SourceBytes = 2_048,
    };

    private static BflMaterial ImageMaterial(string name, string hash) => new()
    {
        Name = name,
        Kind = BflMaterialKind.IMAGE,
        Image = new() { Extension = "png", Width = 2_560, Height = 1_440 },
        SourceHash = hash,
        SourceBytes = 4_096,
    };

    private string MaterialFile(Guid projectId, Guid materialId, string extension) => Path.Combine(this.RootDirectory, projectId.ToString("D"), "materials", $"{materialId:D}{extension}");

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}