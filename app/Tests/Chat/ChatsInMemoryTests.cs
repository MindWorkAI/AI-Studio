using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the chats which are not stored automatically live in memory, one per place.
/// </summary>
/// <remarks>
/// That is the new chat of a place, and the one chat of a place which never stores its chats. The
/// very thread the chat page works with is kept, so that the user finds it again after visiting other
/// chats or pages, together with whether it holds changes the user did not save yet.
/// </remarks>
[TestFixture]
public sealed class ChatsInMemoryTests
{
    private static readonly Guid WORKSPACE_ID = Guid.NewGuid();

    [Test]
    public void AKeptNewChatIsFoundByItsPlace()
    {
        var chatsInMemory = new ChatsInMemory();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };

        chatsInMemory.Keep(newChat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.SameAs(newChat), "Starting a new chat in the workspace opens this one again.");
            Assert.That(chatsInMemory.Find(Guid.Empty), Is.Null, "The chats outside of workspaces are a place of their own.");
            Assert.That(chatsInMemory.Contains(newChat.ChatId), Is.True);
        }
    }

    [Test]
    public void EveryPlaceHasOneNewChat()
    {
        var chatsInMemory = new ChatsInMemory();
        var firstChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        var secondChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };

        chatsInMemory.Keep(firstChat);
        chatsInMemory.Keep(secondChat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.SameAs(secondChat), "A place never holds two new chats.");
            Assert.That(chatsInMemory.GetAll(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void ANewerThreadOfTheSameChatTakesItsPlaceWithItsUnsavedChanges()
    {
        var chatsInMemory = new ChatsInMemory();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        chatsInMemory.Keep(newChat);
        chatsInMemory.SetUnsavedChanges(newChat.ChatId, true);

        // Picking another profile replaces the thread with a copy:
        var copy = newChat with { SelectedProfile = "another profile" };
        chatsInMemory.Follow(copy);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.SameAs(copy), "Keeping the former thread would bring the former profile back.");
            Assert.That(chatsInMemory.HasUnsavedChanges(newChat.ChatId), Is.True, "The copy is the same chat, with the same changes not saved yet.");
        }
    }

    [Test]
    public void FollowingAnotherChatChangesNothing()
    {
        var chatsInMemory = new ChatsInMemory();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        chatsInMemory.Keep(newChat);

        chatsInMemory.Follow(new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID });

        Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.SameAs(newChat), "Only a newer thread of the same chat replaces the one kept.");
    }

    [Test]
    public void AForgottenNewChatIsGone()
    {
        var chatsInMemory = new ChatsInMemory();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        chatsInMemory.Keep(newChat);
        chatsInMemory.SetUnsavedChanges(newChat.ChatId, true);

        chatsInMemory.Forget(newChat.ChatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.Null, "A chat the user sent, emptied, or deleted is no new chat any longer.");
            Assert.That(chatsInMemory.HasUnsavedChanges(newChat.ChatId), Is.False);
        }
    }

    [Test]
    public void DeletingAWorkspaceTakesItsNewChatAlong()
    {
        var chatsInMemory = new ChatsInMemory();
        var newChatInWorkspace = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        var newChatOutside = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = Guid.Empty, IsNewChat = true };
        chatsInMemory.Keep(newChatInWorkspace);
        chatsInMemory.Keep(newChatOutside);

        chatsInMemory.ForgetPlace(WORKSPACE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsInMemory.Find(WORKSPACE_ID), Is.Null, "The new chat went along with its workspace.");
            Assert.That(chatsInMemory.Find(Guid.Empty), Is.SameAs(newChatOutside), "The new chats of other places stay.");
        }
    }
}