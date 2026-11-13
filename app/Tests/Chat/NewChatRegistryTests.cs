using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the new chats of the places which do not store their chats automatically live in memory.
/// </summary>
/// <remarks>
/// Every place has at most one new chat. The registry holds the very thread the chat page works with,
/// so that the user finds it again after visiting other chats or pages, together with whether it holds
/// changes the user did not save yet.
/// </remarks>
[TestFixture]
public sealed class NewChatRegistryTests
{
    private static readonly Guid WORKSPACE_ID = Guid.NewGuid();

    [Test]
    public void AKeptNewChatIsFoundByItsPlace()
    {
        var registry = new NewChatRegistry();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };

        registry.Keep(newChat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Find(WORKSPACE_ID), Is.SameAs(newChat), "Starting a new chat in the workspace opens this one again.");
            Assert.That(registry.Find(Guid.Empty), Is.Null, "The chats outside of workspaces are a place of their own.");
            Assert.That(registry.Contains(newChat.ChatId), Is.True);
        }
    }

    [Test]
    public void EveryPlaceHasOneNewChat()
    {
        var registry = new NewChatRegistry();
        var firstChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        var secondChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };

        registry.Keep(firstChat);
        registry.Keep(secondChat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Find(WORKSPACE_ID), Is.SameAs(secondChat), "A place never holds two new chats.");
            Assert.That(registry.GetAll(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void ANewerThreadOfTheSameChatTakesItsPlaceWithItsUnsavedChanges()
    {
        var registry = new NewChatRegistry();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        registry.Keep(newChat);
        registry.SetUnsavedChanges(newChat.ChatId, true);

        // Picking another profile replaces the thread with a copy:
        var copy = newChat with { SelectedProfile = "another profile" };
        registry.Follow(copy);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Find(WORKSPACE_ID), Is.SameAs(copy), "Keeping the former thread would bring the former profile back.");
            Assert.That(registry.HasUnsavedChanges(newChat.ChatId), Is.True, "The copy is the same chat, with the same changes not saved yet.");
        }
    }

    [Test]
    public void FollowingAnotherChatChangesNothing()
    {
        var registry = new NewChatRegistry();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        registry.Keep(newChat);

        registry.Follow(new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID });

        Assert.That(registry.Find(WORKSPACE_ID), Is.SameAs(newChat), "Only a newer thread of the same chat replaces the one kept.");
    }

    [Test]
    public void AForgottenNewChatIsGone()
    {
        var registry = new NewChatRegistry();
        var newChat = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        registry.Keep(newChat);
        registry.SetUnsavedChanges(newChat.ChatId, true);

        registry.Forget(newChat.ChatId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Find(WORKSPACE_ID), Is.Null, "A chat the user sent, emptied, or deleted is no new chat any longer.");
            Assert.That(registry.HasUnsavedChanges(newChat.ChatId), Is.False);
        }
    }

    [Test]
    public void DeletingAWorkspaceTakesItsNewChatAlong()
    {
        var registry = new NewChatRegistry();
        var newChatInWorkspace = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = WORKSPACE_ID, IsNewChat = true };
        var newChatOutside = new ChatThread { ChatId = Guid.NewGuid(), WorkspaceId = Guid.Empty, IsNewChat = true };
        registry.Keep(newChatInWorkspace);
        registry.Keep(newChatOutside);

        registry.ForgetPlace(WORKSPACE_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(registry.Find(WORKSPACE_ID), Is.Null, "The new chat went along with its workspace.");
            Assert.That(registry.Find(Guid.Empty), Is.SameAs(newChatOutside), "The new chats of other places stay.");
        }
    }
}