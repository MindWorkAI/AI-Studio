using AIStudio.Chat;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that the provenance store keeps what stored chats refer to, and nothing else.
/// </summary>
/// <remarks>
/// What a chat received belongs to it: kept as long as the chat refers to it, removed once it no
/// longer does. The cleanup at the start must never take what this run stored since, because a
/// chat in memory may refer to it before its file does.
/// </remarks>
[TestFixture]
public sealed class ProvenanceCleanupTests
{
    private static readonly CancellationToken TOKEN = CancellationToken.None;

    private static readonly Func<Guid, Guid, Task<ChatThread?>> UNREADABLE = (_, _) => Task.FromResult<ChatThread?>(null);

    [Test]
    public void AChatRefersToItsPassagesItsAttachmentsAndItsDraft()
    {
        var passageId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var draftAttachmentId = Guid.NewGuid();
        var previewedTextId = Guid.NewGuid();
        var chat = ChatWith(passageId, attachmentId);

        // A message being edited brings its attachment along, and a preview read another one:
        chat.Draft.AddAttachments([new FileAttachment(FileAttachmentType.DOCUMENT, "draft.txt", "/tmp/draft.txt", 12) { ProvenanceId = draftAttachmentId }]);
        chat.Draft.NotePreviewedText(new("/tmp/notes.txt", previewedTextId, new(DateTime.UtcNow, 12)));

        Assert.That(chat.CollectProvenanceIds(), Is.EquivalentTo(new[] { passageId, attachmentId, draftAttachmentId, previewedTextId }), "The message being written is referred to as well, or the cleanup would take the texts it sends.");
    }

    [Test]
    public async Task WhatNoChatRefersToIsRemoved()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var keptChatId = Guid.NewGuid();
        var deletedChatId = Guid.NewGuid();
        var referenced = await store.Client.StoreAsync(keptChatId, ProvenanceKind.DATA_SOURCE_PASSAGE, "still referred to", null, TOKEN);
        var ofRemovedMessage = await store.Client.StoreAsync(keptChatId, ProvenanceKind.DATA_SOURCE_PASSAGE, "of a removed message", null, TOKEN);
        var ofDeletedChat = await store.Client.StoreAsync(deletedChatId, ProvenanceKind.WEB_PAGE, "of a deleted chat", null, TOKEN);
        var chat = ChatWith(referenced!.Value, attachmentId: null, keptChatId);

        var result = await CleanUpAsync(store, Locations(keptChatId), LoaderOf(chat), DateTimeOffset.UtcNow.AddMinutes(1));
        var entries = await store.Client.GetEntriesAsync([referenced.Value, ofRemovedMessage!.Value, ofDeletedChat!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { referenced.Value }));
            Assert.That(result.Orphaned, Is.EqualTo(1));
            Assert.That(result.Unreferenced, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task WhatThisRunStoredStays()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var openChatId = Guid.NewGuid();
        var newChatId = Guid.NewGuid();
        var previewed = await store.Client.StoreAsync(openChatId, ProvenanceKind.ATTACHMENT, "previewed, not yet in the chat file", null, TOKEN);
        var ofNewChat = await store.Client.StoreAsync(newChatId, ProvenanceKind.ATTACHMENT, "of a chat not stored yet", null, TOKEN);

        await CleanUpAsync(store, Locations(openChatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, openChatId)), startedAt);
        var entries = await store.Client.GetEntriesAsync([previewed!.Value, ofNewChat!.Value], TOKEN);

        Assert.That(entries.Keys, Is.EquivalentTo(new[] { previewed.Value, ofNewChat.Value }), "A chat in memory may refer to entries its file does not know yet.");
    }

    [Test]
    public async Task WhatWasStoredForAChatWithoutAnIdIsRemoved()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var entry = await store.Client.StoreAsync(Guid.Empty, ProvenanceKind.WEB_PAGE, "of an assistant's conversation without an id", null, TOKEN);

        var result = await CleanUpAsync(store, Locations(Guid.NewGuid()), UNREADABLE, DateTimeOffset.UtcNow.AddMinutes(1));
        var entries = await store.Client.GetEntriesAsync([entry!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries, Is.Empty, "No chat on the disk has no id, so nothing would ever remove such an entry.");
            Assert.That(result.Orphaned, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task AChatWhichCannotBeReadKeepsEverything()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var entry = await store.Client.StoreAsync(chatId, ProvenanceKind.MAIL, "a mail", null, TOKEN);

        var result = await CleanUpAsync(store, Locations(chatId), UNREADABLE, DateTimeOffset.UtcNow.AddMinutes(1));
        var entries = await store.Client.GetEntriesAsync([entry!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { entry.Value }), "Nothing tells what an unreadable chat refers to, e.g., one written by a newer version.");
            Assert.That(result.Unreadable, Is.EqualTo(1), "The next start has to read it again.");
        }
    }

    [Test]
    public async Task AChatWhichDidNotChangeSinceTheLastCleanupIsNotRead()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var entry = await store.Client.StoreAsync(chatId, ProvenanceKind.ATTACHMENT, "kept by the last cleanup", null, TOKEN);
        var lastCleanup = DateTimeOffset.UtcNow.AddMinutes(1);
        var reads = new List<Guid>();

        var result = await CleanUpAsync(store, Locations(chatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, chatId), reads), lastCleanup.AddMinutes(1), lastCleanup, writtenAt: lastCleanup.AddHours(-1));
        var entries = await store.Client.GetEntriesAsync([entry!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.Empty, "Reading every chat at every start costs much on a small computer.");
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { entry.Value }), "Skipping a chat never removes anything.");
        }
    }

    [Test]
    public async Task AChatWhichReceivedAnEntrySinceTheLastCleanupIsRead()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var lastCleanup = DateTimeOffset.UtcNow.AddMinutes(-1);
        var chatId = Guid.NewGuid();
        var previewed = await store.Client.StoreAsync(chatId, ProvenanceKind.ATTACHMENT, "previewed, but never sent", null, TOKEN);
        var reads = new List<Guid>();

        await CleanUpAsync(store, Locations(chatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, chatId), reads), DateTimeOffset.UtcNow.AddMinutes(1), lastCleanup, writtenAt: lastCleanup.AddHours(-1));
        var entries = await store.Client.GetEntriesAsync([previewed!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EquivalentTo(new[] { chatId }), "The last cleanup had to leave alone what was stored after it began.");
            Assert.That(entries, Is.Empty);
        }
    }

    [TestCase(60, TestName = "AChatWrittenSinceTheLastCleanupIsRead")]
    [TestCase(-2, TestName = "AChatWrittenShortlyBeforeTheLastCleanupIsRead")]
    public async Task AChatWrittenAroundTheLastCleanupIsRead(int secondsAfterTheLastCleanup)
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var ofRemovedMessage = await store.Client.StoreAsync(chatId, ProvenanceKind.DATA_SOURCE_PASSAGE, "of a message removed after the last cleanup", null, TOKEN);
        var lastCleanup = DateTimeOffset.UtcNow.AddMinutes(1);
        var reads = new List<Guid>();

        // Shortly before counts as well: the time of a file may be coarse, e.g., two seconds on FAT.
        await CleanUpAsync(store, Locations(chatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, chatId), reads), lastCleanup.AddMinutes(1), lastCleanup, writtenAt: lastCleanup.AddSeconds(secondsAfterTheLastCleanup));
        var entries = await store.Client.GetEntriesAsync([ofRemovedMessage!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reads, Is.EquivalentTo(new[] { chatId }));
            Assert.That(entries, Is.Empty);
        }
    }

    [Test]
    public async Task AChatWhoseTimeOfWritingIsNotKnownIsRead()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        await store.Client.StoreAsync(chatId, ProvenanceKind.MAIL, "a mail", null, TOKEN);
        var lastCleanup = DateTimeOffset.UtcNow.AddMinutes(1);
        var reads = new List<Guid>();

        await CleanUpAsync(store, Locations(chatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, chatId), reads), lastCleanup.AddMinutes(1), lastCleanup, writtenAt: null);

        Assert.That(reads, Is.EquivalentTo(new[] { chatId }), "Only a chat known to be unchanged may be skipped.");
    }

    [Test]
    public async Task AChatWhichNoLongerExistsIsRemovedAfterEveryCleanup()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var entry = await store.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.WEB_PAGE, "of a chat deleted while the store was not available", null, TOKEN);
        var lastCleanup = DateTimeOffset.UtcNow.AddMinutes(1);
        var reads = new List<Guid>();

        var result = await CleanUpAsync(store, Locations(), LoaderOf(ChatWith(passageId: null, attachmentId: null), reads), lastCleanup.AddMinutes(1), lastCleanup, writtenAt: lastCleanup.AddHours(-1));
        var entries = await store.Client.GetEntriesAsync([entry!.Value], TOKEN);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries, Is.Empty, "Telling that a chat no longer exists costs no reading.");
            Assert.That(result.Orphaned, Is.EqualTo(1));
            Assert.That(reads, Is.Empty);
        }
    }

    private static ChatThread ChatWith(Guid? passageId, Guid? attachmentId, Guid? chatId = null) => new()
    {
        ChatId = chatId ?? Guid.NewGuid(),
        Blocks =
        [
            new ContentBlock
            {
                ContentType = ContentType.TEXT,
                Role = ChatRole.USER,
                Content = new ContentText
                {
                    Text = "Summarize the minutes.",
                    FileAttachments = attachmentId is null ? [] : [new FileAttachment(FileAttachmentType.DOCUMENT, "minutes.txt", "/tmp/minutes.txt", 24) { ProvenanceId = attachmentId }],
                },
            },
            new ContentBlock
            {
                ContentType = ContentType.TEXT,
                Role = ChatRole.AI,
                Content = new ContentText
                {
                    Text = "The board met on Monday.",
                    SourcePassages = passageId is null ? [] : [new SourcePassage { SourceUrl = "file:///C:/docs/minutes.pdf", ProvenanceId = passageId }],
                },
            },
        ],
    };

    private static IReadOnlyDictionary<Guid, Guid> Locations(params Guid[] chatIds) => chatIds.ToDictionary(chatId => chatId, _ => Guid.Empty);

    /// <summary>
    /// Reads the given chat, and notes every chat it was asked for.
    /// </summary>
    private static Func<Guid, Guid, Task<ChatThread?>> LoaderOf(ChatThread chat, List<Guid>? reads = null) => (_, chatId) =>
    {
        reads?.Add(chatId);
        return Task.FromResult<ChatThread?>(chatId == chat.ChatId ? chat : null);
    };

    /// <summary>
    /// Runs the cleanup on chats which were all written at the same moment.
    /// </summary>
    private static Task<ProvenanceCleanupResult> CleanUpAsync(TemporaryProvenanceStore store, IReadOnlyDictionary<Guid, Guid> chatLocations, Func<Guid, Guid, Task<ChatThread?>> loadChat, DateTimeOffset createdBefore, DateTimeOffset? lastCleanup = null, DateTimeOffset? writtenAt = null) =>
        ProvenanceCleanup.CleanUpAsync(store.Client, chatLocations, loadChat, (_, _) => writtenAt, createdBefore, lastCleanup, TOKEN);
}