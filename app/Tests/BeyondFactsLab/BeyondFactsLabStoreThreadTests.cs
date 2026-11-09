using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab stores the chats of the steps.
/// </summary>
[TestFixture]
public sealed class BeyondFactsLabStoreThreadTests
{
    private string dataDirectory = string.Empty;

    private string RootDirectory => Path.Combine(this.dataDirectory, BeyondFactsLabStore.ROOT_DIRECTORY_NAME);

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-threads-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task AStepWithoutAChatHasNone()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");

        var thread = await store.LoadThreadAsync(new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK));

        Assert.That(thread, Is.Null);
    }

    [Test]
    public async Task AStoredChatIsLoadedWithItsMessages()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflThreadLocation(created.ProjectId, BflStep.KNOWLEDGE);
        var thread = WithMessage(BflThreadFactory.Create(location), "Worauf beruht meine Expertise?");

        var changed = await store.SaveThreadAsync(location, thread);
        var loaded = await this.CreateStore().LoadThreadAsync(location);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.ChatId, Is.EqualTo(location.ChatId));
            Assert.That(loaded.Blocks, Has.Count.EqualTo(1));
            Assert.That(((ContentText)loaded.Blocks[0].Content!).Text, Is.EqualTo("Worauf beruht meine Expertise?"));
            Assert.That(changed.ModifiedAtUtc, Is.GreaterThan(created.ModifiedAtUtc), "A conversation is work on the preparation.");
            Assert.That(File.Exists(Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"), "steps", "02-knowledge", "thread.json")), Is.True);
        }
    }

    [Test]
    public async Task AChatOfAnotherStepIsNotStored()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var thread = BflThreadFactory.Create(new BflThreadLocation(created.ProjectId, BflStep.STRATEGY));

        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SaveThreadAsync(new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK), thread));
    }

    [Test]
    public async Task AChatOfADeletedPreparationIsNotStored()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        await store.DeleteAsync(created.ProjectId);

        await Assert.ThrowsAsync<FileNotFoundException>(async () => await store.SaveThreadAsync(location, BflThreadFactory.Create(location)));

        Assert.That(Directory.Exists(Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"))), Is.False, "An answer which ends after the deletion must not bring the preparation back.");
    }

    [Test]
    public async Task AChatCopiedIntoAnotherStepIsNotOpened()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var strategy = new BflThreadLocation(created.ProjectId, BflStep.STRATEGY);
        var framework = new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        await store.SaveThreadAsync(strategy, BflThreadFactory.Create(strategy));
        var stepsDirectory = Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"), "steps");
        Directory.CreateDirectory(Path.Combine(stepsDirectory, "01-framework"));
        File.Copy(Path.Combine(stepsDirectory, "05-strategy", "thread.json"), Path.Combine(stepsDirectory, "01-framework", "thread.json"));

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadThreadAsync(framework));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveThreadAsync(framework, BflThreadFactory.Create(framework)));
        }
    }

    [Test]
    public async Task ADamagedChatIsNeitherOpenedNorOverwritten()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        var path = Path.Combine(this.RootDirectory, created.ProjectId.ToString("D"), "steps", "01-framework", "thread.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{ damaged");

        using (Assert.EnterMultipleScope())
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.LoadThreadAsync(location));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await store.SaveThreadAsync(location, BflThreadFactory.Create(location)));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("{ damaged"));
        }
    }

    [Test]
    public async Task StartingOverDeletesTheChat()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Panel");
        var location = new BflThreadLocation(created.ProjectId, BflStep.FRAMEWORK);
        await store.SaveThreadAsync(location, WithMessage(BflThreadFactory.Create(location), "Hallo"));

        await store.DeleteThreadAsync(location);

        Assert.That(await store.LoadThreadAsync(location), Is.Null);
    }

    [Test]
    public async Task EveryChatOnTheDiskIsFoundByItsId()
    {
        var store = this.CreateStore();
        var first = await store.CreateAsync("Panel");
        var second = await store.CreateAsync("Interview");
        var firstStrategy = new BflThreadLocation(first.ProjectId, BflStep.STRATEGY);
        var secondFramework = new BflThreadLocation(second.ProjectId, BflStep.FRAMEWORK);
        await store.SaveThreadAsync(firstStrategy, BflThreadFactory.Create(firstStrategy));
        await store.SaveThreadAsync(secondFramework, BflThreadFactory.Create(secondFramework));

        // A preparation this version cannot open keeps its chats all the same:
        await File.WriteAllTextAsync(Path.Combine(this.RootDirectory, second.ProjectId.ToString("D"), "project.json"), "{ damaged");

        var locations = this.CreateStore().ReadAllThreadLocations();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(locations, Has.Count.EqualTo(2));
            Assert.That(locations[firstStrategy.ChatId], Is.EqualTo(firstStrategy));
            Assert.That(locations[secondFramework.ChatId], Is.EqualTo(secondFramework));
            Assert.That(store.GetThreadWrittenAt(firstStrategy), Is.Not.Null);
            Assert.That(store.GetThreadWrittenAt(new BflThreadLocation(first.ProjectId, BflStep.FRAMEWORK)), Is.Null);
        }
    }

    private static ChatThread WithMessage(ChatThread thread, string text)
    {
        thread.Blocks.Add(new ContentBlock
        {
            Time = DateTimeOffset.Now,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText { Text = text }
        });

        return thread;
    }

    private BeyondFactsLabStore CreateStore() => new(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
}