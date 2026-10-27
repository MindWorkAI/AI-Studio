using AIStudio.Tools.Glossaries;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Glossaries;

/// <summary>
/// Checks how the entries of the glossaries are kept on disk.
/// </summary>
/// <remarks>
/// The settings describe a glossary, while its entries live in a file of their own below the data
/// directory. Entries saved once have to come back after a restart, an unreadable file must neither
/// break the app nor be overwritten behind the user's back, and an ID must never lead outside the
/// folder of the glossaries.
/// </remarks>
[TestFixture]
public sealed class GlossaryStoreTests
{
    private string dataDirectory = string.Empty;

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-glossaries-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        try
        {
            Directory.Delete(this.dataDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task SavedEntriesComeBackAfterARestart()
    {
        var glossaryId = Guid.NewGuid().ToString();
        GlossaryEntry[] entries =
        [
            new("Arbeitszeit", "working time"),
            new("Muggel", "A person without magical abilities.", ["Muggels", "Muggle"], "From the Harry Potter books."),
        ];

        await this.CreateStore().SaveEntriesAsync(glossaryId, entries);
        var loaded = await this.CreateStore().LoadEntriesAsync(glossaryId);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Has.Count.EqualTo(2));
            Assert.That(loaded[0], Is.EqualTo(entries[0]).Using<GlossaryEntry>(AreSame));
            Assert.That(loaded[1], Is.EqualTo(entries[1]).Using<GlossaryEntry>(AreSame));
        });
    }

    [Test]
    public async Task AGlossaryWithoutAFileHasNoEntries()
    {
        var loaded = await this.CreateStore().LoadEntriesAsync(Guid.NewGuid().ToString());

        Assert.That(loaded, Is.Empty);
    }

    [Test]
    public async Task DeletingAGlossaryRemovesItsFile()
    {
        var store = this.CreateStore();
        var glossaryId = Guid.NewGuid().ToString();
        await store.SaveEntriesAsync(glossaryId, [new GlossaryEntry("Glossar", "glossary")]);

        await store.DeleteEntriesAsync(glossaryId);
        var loaded = await store.LoadEntriesAsync(glossaryId);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Empty, "The cache must not keep the entries of a deleted glossary.");
            Assert.That(Directory.GetFiles(this.GlossaryFolder), Is.Empty);
        });
    }

    [Test]
    public void DeletingAGlossaryWithoutAFileSucceeds()
    {
        var store = this.CreateStore();

        Assert.DoesNotThrowAsync(async () => await store.DeleteEntriesAsync(Guid.NewGuid().ToString()), "A glossary whose entries were never saved has no file, and maybe not even the folder of the glossaries.");
    }

    [Test]
    public async Task AnUnreadableFileGivesNoEntriesAndStaysAsItIs()
    {
        var glossaryId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(this.GlossaryFolder);
        var path = Path.Combine(this.GlossaryFolder, $"{glossaryId}.json");
        await File.WriteAllTextAsync(path, "{ not json");

        var store = this.CreateStore();
        var loaded = await store.LoadEntriesAsync(glossaryId);
        var loadedForEditing = await store.TryLoadEntriesAsync(glossaryId);
        var fileContent = await File.ReadAllTextAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Empty);
            Assert.That(loadedForEditing, Is.Null, "Whoever edits the glossary has to tell an unreadable file from an empty glossary, or saving would overwrite the entries.");
            Assert.That(fileContent, Is.EqualTo("{ not json"),"The user may still repair the file, so reading it must not change it.");
        });
    }

    [Test]
    public async Task TheEntriesOfAPluginGlossaryComeFromMemoryAndCannotBeChanged()
    {
        var store = this.CreateStore();
        var glossaryId = Guid.NewGuid().ToString();
        store.SetProvidedEntries(glossaryId, [new GlossaryEntry("Arbeitszeit", "working time")]);

        var loaded = await store.LoadEntriesAsync(glossaryId);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Select(entry => entry.Term), Is.EqualTo(new[] { "Arbeitszeit" }));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveEntriesAsync(glossaryId, []), "The plugin is the source of these entries.");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await store.DeleteEntriesAsync(glossaryId));
            Assert.That(Directory.Exists(this.GlossaryFolder), Is.False, "A glossary of a plugin leaves no file behind.");
        });
    }

    [Test]
    public async Task TheEntriesOfAGlossaryWhosePluginIsGoneAreForgotten()
    {
        var store = this.CreateStore();
        var keptId = Guid.NewGuid().ToString();
        var goneId = Guid.NewGuid().ToString();
        store.SetProvidedEntries(keptId, [new GlossaryEntry("Arbeitszeit", "working time")]);
        store.SetProvidedEntries(goneId, [new GlossaryEntry("Urlaub", "vacation")]);

        store.RetainProvidedEntries([keptId]);

        var kept = await store.LoadEntriesAsync(keptId);
        var gone = await store.LoadEntriesAsync(goneId);

        Assert.Multiple(() =>
        {
            Assert.That(kept, Has.Count.EqualTo(1));
            Assert.That(gone, Is.Empty);
        });
    }

    [TestCase("../settings")]
    [TestCase("..\\..\\settings")]
    [TestCase("")]
    public void AnIdWhichIsNoGuidIsRefused(string glossaryId)
    {
        var store = this.CreateStore();

        Assert.ThrowsAsync<ArgumentException>(async () => await store.SaveEntriesAsync(glossaryId, []));
    }

    private string GlossaryFolder => Path.Combine(this.dataDirectory, GlossaryStore.FOLDER_NAME);

    private GlossaryStore CreateStore() => new(NullLogger<GlossaryStore>.Instance, this.dataDirectory);

    //
    // Records compare lists by reference, so the aliases need a comparison of their own:
    //
    private static bool AreSame(GlossaryEntry expected, GlossaryEntry actual) =>
        expected.Term == actual.Term
        && expected.Value == actual.Value
        && expected.Note == actual.Note
        && (expected.Aliases ?? []).SequenceEqual(actual.Aliases ?? []);
}