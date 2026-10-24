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
        chat.Draft.NotePreviewedText(new("/tmp/notes.txt", previewedTextId, DateTime.UtcNow));

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

        var (orphaned, unreferenced) = await ProvenanceCleanup.CleanUpAsync(store.Client, Locations(keptChatId), LoaderOf(chat), DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        var entries = await store.Client.GetEntriesAsync([referenced.Value, ofRemovedMessage!.Value, ofDeletedChat!.Value], TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(entries.Keys, Is.EquivalentTo(new[] { referenced.Value }));
            Assert.That(orphaned, Is.EqualTo(1));
            Assert.That(unreferenced, Is.EqualTo(1));
        });
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

        await ProvenanceCleanup.CleanUpAsync(store.Client, Locations(openChatId), LoaderOf(ChatWith(passageId: null, attachmentId: null, openChatId)), startedAt, TOKEN);
        var entries = await store.Client.GetEntriesAsync([previewed!.Value, ofNewChat!.Value], TOKEN);

        Assert.That(entries.Keys, Is.EquivalentTo(new[] { previewed.Value, ofNewChat.Value }), "A chat in memory may refer to entries its file does not know yet.");
    }

    [Test]
    public async Task AChatWhichCannotBeReadKeepsEverything()
    {
        await using var store = await TemporaryProvenanceStore.CreateAsync();
        var chatId = Guid.NewGuid();
        var entry = await store.Client.StoreAsync(chatId, ProvenanceKind.MAIL, "a mail", null, TOKEN);

        await ProvenanceCleanup.CleanUpAsync(store.Client, Locations(chatId), (_, _) => Task.FromResult<ChatThread?>(null), DateTimeOffset.UtcNow.AddMinutes(1), TOKEN);
        var entries = await store.Client.GetEntriesAsync([entry!.Value], TOKEN);

        Assert.That(entries.Keys, Is.EquivalentTo(new[] { entry.Value }), "Nothing tells what an unreadable chat refers to, e.g., one written by a newer version.");
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

    private static Func<Guid, Guid, Task<ChatThread?>> LoaderOf(ChatThread chat) => (_, chatId) => Task.FromResult<ChatThread?>(chatId == chat.ChatId ? chat : null);
}