using AIStudio.Chat;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks when a chat gets stored with the message the user has not sent yet.
/// </summary>
/// <remarks>
/// Storing writes the whole conversation, so it waits until the user paused, and it happens once more
/// when the user leaves the chat. A chat being answered is stored by its AI job instead, and a chat which
/// is not on the disk is left to whoever creates it: when it is gone, it was deleted.
/// </remarks>
[TestFixture]
public sealed class ChatDraftAutosaveTests
{
    private static readonly TimeSpan SHORT_DELAY = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LONG_DELAY = TimeSpan.FromHours(1);
    private static readonly TimeSpan WAIT_LIMIT = TimeSpan.FromSeconds(10);

    [Test]
    public async Task ManyChangesInARowAreStoredOnce()
    {
        var storage = new FakeStorage();
        await using var autosave = storage.CreateAutosave(SHORT_DELAY);
        var chat = new ChatThread { ChatId = Guid.NewGuid() };

        for (var keystroke = 0; keystroke < 5; keystroke++)
            autosave.Schedule(chat);

        await storage.WaitForStoreAsync();
        await Task.Delay(SHORT_DELAY * 4);

        Assert.That(storage.StoredChats, Has.Count.EqualTo(1), "Every change starts the waiting anew, so typing a word writes the chat once, not once per letter.");
    }

    [Test]
    public async Task LeavingTheChatStoresItRightAway()
    {
        var storage = new FakeStorage();
        await using var autosave = storage.CreateAutosave(LONG_DELAY);
        var chat = new ChatThread { ChatId = Guid.NewGuid() };

        autosave.Schedule(chat);
        await autosave.FlushAsync();

        Assert.That(storage.StoredChats, Is.EqualTo(new[] { chat }), "The chat being left has to be on the disk before the next one opens.");
    }

    [Test]
    public async Task AChatBeingAnsweredIsStoredOnceTheAnswerIsDone()
    {
        var storage = new FakeStorage { IsBusy = true };
        await using var autosave = storage.CreateAutosave(SHORT_DELAY);
        var chat = new ChatThread { ChatId = Guid.NewGuid() };

        autosave.Schedule(chat);
        await Task.Delay(SHORT_DELAY * 4);
        var storedWhileBusy = storage.StoredChats.Count;

        storage.IsBusy = false;
        await storage.WaitForStoreAsync();

        Assert.Multiple(() =>
        {
            Assert.That(storedWhileBusy, Is.Zero, "The AI job changes the conversation meanwhile, so storing it from elsewhere could read it in the middle of a change.");
            Assert.That(storage.StoredChats, Is.EqualTo(new[] { chat }), "Once the answer is done, the change must still reach the disk.");
        });
    }

    [Test]
    public async Task LeavingAChatBeingAnsweredLeavesItToTheJob()
    {
        var storage = new FakeStorage { IsBusy = true };
        await using var autosave = storage.CreateAutosave(LONG_DELAY);

        autosave.Schedule(new ChatThread { ChatId = Guid.NewGuid() });
        await autosave.FlushAsync();

        Assert.That(storage.StoredChats, Is.Empty, "The AI job stores the chat once more when the answer is done, together with the draft it shares.");
    }

    [Test]
    public async Task AChatWhichIsNotOnTheDiskIsNeverStored()
    {
        var storage = new FakeStorage { IsStored = false };
        await using var autosave = storage.CreateAutosave(SHORT_DELAY);

        autosave.Schedule(new ChatThread { ChatId = Guid.NewGuid() });
        await Task.Delay(SHORT_DELAY * 4);
        await autosave.FlushAsync();

        Assert.That(storage.StoredChats, Is.Empty, "A chat gone from the disk was deleted, and storing it would bring it back.");
    }

    [Test]
    public async Task TheNewestCopyOfTheChatIsStored()
    {
        var storage = new FakeStorage();
        await using var autosave = storage.CreateAutosave(LONG_DELAY);
        var chat = new ChatThread { ChatId = Guid.NewGuid() };

        autosave.Schedule(chat);

        // What the chat does when the user picks another profile:
        var withOtherProfile = chat with { SelectedProfile = Guid.NewGuid().ToString() };
        autosave.Follow(withOtherProfile);
        await autosave.FlushAsync();

        Assert.That(storage.StoredChats.Single(), Is.SameAs(withOtherProfile), "Storing the former copy would bring the former profile back.");
    }

    [Test]
    public async Task AChangeInAnotherChatStoresThePreviousOne()
    {
        var storage = new FakeStorage();
        await using var autosave = storage.CreateAutosave(LONG_DELAY);
        var previousChat = new ChatThread { ChatId = Guid.NewGuid() };

        autosave.Schedule(previousChat);
        autosave.Schedule(new ChatThread { ChatId = Guid.NewGuid() });
        await storage.WaitForStoreAsync();

        Assert.That(storage.StoredChats.Single(), Is.SameAs(previousChat), "The change in one chat must not cost the other its draft.");
    }

    [Test]
    public async Task DisposingStoresWhatIsPending()
    {
        var storage = new FakeStorage();
        var autosave = storage.CreateAutosave(LONG_DELAY);
        var chat = new ChatThread { ChatId = Guid.NewGuid() };

        autosave.Schedule(chat);
        await autosave.DisposeAsync();
        autosave.Schedule(new ChatThread { ChatId = Guid.NewGuid() });
        await autosave.FlushAsync();

        Assert.That(storage.StoredChats, Is.EqualTo(new[] { chat }), "Leaving the chat page disposes the chat, which counts as leaving the chat. What comes later belongs to no chat on the screen.");
    }

    /// <summary>
    /// Stands in for the disk and for the AI jobs, and records which chats were stored.
    /// </summary>
    private sealed class FakeStorage
    {
        private readonly Lock storedLock = new();
        private readonly List<ChatThread> storedChats = [];
        private readonly SemaphoreSlim storeSignal = new(0);
        private volatile bool isBusy;
        private volatile bool isStored = true;

        public bool IsBusy
        {
            get => this.isBusy;
            set => this.isBusy = value;
        }

        public bool IsStored
        {
            get => this.isStored;
            init => this.isStored = value;
        }

        public IReadOnlyList<ChatThread> StoredChats
        {
            get
            {
                lock (this.storedLock)
                    return [..this.storedChats];
            }
        }

        public ChatDraftAutosave CreateAutosave(TimeSpan delay) => new(delay, _ => this.isStored, _ => this.isBusy, this.StoreAsync, NullLogger.Instance);

        public async Task WaitForStoreAsync()
        {
            if (!await this.storeSignal.WaitAsync(WAIT_LIMIT))
                Assert.Fail("The chat was never stored.");
        }

        private Task<bool> StoreAsync(ChatThread chat)
        {
            lock (this.storedLock)
                this.storedChats.Add(chat);

            this.storeSignal.Release();
            return Task.FromResult(true);
        }
    }
}