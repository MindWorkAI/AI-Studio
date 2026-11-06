using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks that storing chats and using workspaces are two settings of their own, and that settings
/// files of older versions keep what they meant.
/// </summary>
/// <remarks>
/// Until workspaces had a switch of their own, switching them off also stopped storing chats. The
/// stored value kept its name, so that older versions of AI Studio still read the settings file. The
/// app asks <see cref="DataWorkspace.AreChatsStored"/>, <see cref="DataWorkspace.UsesWorkspaces"/>,
/// <see cref="DataWorkspace.CanChatsGoIntoWorkspaces"/>, and <see cref="DataWorkspace.IsChatListShown"/>
/// instead.
/// </remarks>
[TestFixture]
public sealed class WorkspaceSettingsTests
{
    [TestCase(WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY, true, true, true, true)]
    [TestCase(WorkspaceStorageBehavior.STORE_CHATS_MANUALLY, true, true, true, true)]
    [TestCase(WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY, false, true, false, true)]
    [TestCase(WorkspaceStorageBehavior.DISABLE_WORKSPACES, true, false, false, true)]
    [TestCase(WorkspaceStorageBehavior.DISABLE_WORKSPACES, false, false, false, false)]
    public void StoringAndWorkspacesAreChosenApart(WorkspaceStorageBehavior storageBehavior, bool areWorkspacesEnabled, bool expectChatsStored, bool expectChatsIntoWorkspaces, bool expectChatList)
    {
        var workspace = new DataWorkspace
        {
            StorageBehavior = storageBehavior,
            AreWorkspacesEnabled = areWorkspacesEnabled,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.AreChatsStored, Is.EqualTo(expectChatsStored));
            Assert.That(workspace.UsesWorkspaces, Is.EqualTo(areWorkspacesEnabled), "Workspaces follow their own switch, whether chats are stored or not.");
            Assert.That(workspace.CanChatsGoIntoWorkspaces, Is.EqualTo(expectChatsIntoWorkspaces), "Putting a chat into a workspace stores it, which needs stored chats.");
            Assert.That(workspace.IsChatListShown, Is.EqualTo(expectChatList));
        }
    }

    [Test]
    public void ASettingsFileWithoutTheSwitchKeepsUsingWorkspaces()
    {
        var workspace = Read("""{ "StorageBehavior": "STORE_CHATS_AUTOMATICALLY" }""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.AreWorkspacesEnabled, Is.Null);
            Assert.That(workspace.UsesWorkspaces, Is.True, "A settings file of an older version has no switch for workspaces, and its workspaces must stay.");
            Assert.That(workspace.CanChatsGoIntoWorkspaces, Is.True);
        }
    }

    [Test]
    public void ASettingsFileWithDisabledWorkspacesKeepsBoth()
    {
        var workspace = Read("""{ "StorageBehavior": "DISABLE_WORKSPACES" }""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.AreChatsStored, Is.False, "Whoever switched workspaces off before also chose to store no chats, and must not find their chats stored now.");
            Assert.That(workspace.UsesWorkspaces, Is.False, "Whoever switched workspaces off before must not find them back.");
            Assert.That(workspace.IsChatListShown, Is.False);
        }
    }

    [Test]
    public void TheSwitchWinsOnceTheUserUsedIt()
    {
        var workspace = Read("""{ "StorageBehavior": "DISABLE_WORKSPACES", "AreWorkspacesEnabled": true }""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.UsesWorkspaces, Is.True);
            Assert.That(workspace.CanChatsGoIntoWorkspaces, Is.False, "While no chat is stored, no new chat goes into a workspace.");
            Assert.That(workspace.IsChatListShown, Is.True, "The sidebar lists the workspaces and what they hold.");
        }
    }

    [Test]
    public void OnlyTheChoicesAreStored()
    {
        var json = JsonSerializer.Serialize(new DataWorkspace { AreWorkspacesEnabled = false }, SettingsManager.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Does.Contain($"\"{nameof(DataWorkspace.AreWorkspacesEnabled)}\": false"));
            Assert.That(json, Does.Not.Contain(nameof(DataWorkspace.AreChatsStored)), "What follows from the choices is not stored with them.");
            Assert.That(json, Does.Not.Contain(nameof(DataWorkspace.UsesWorkspaces)), "What follows from the choices is not stored with them.");
            Assert.That(json, Does.Not.Contain(nameof(DataWorkspace.CanChatsGoIntoWorkspaces)), "What follows from the choices is not stored with them.");
            Assert.That(json, Does.Not.Contain(nameof(DataWorkspace.IsChatListShown)), "What follows from the choices is not stored with them.");
        }
    }

    private static DataWorkspace Read(string json) => JsonSerializer.Deserialize<DataWorkspace>(json, SettingsManager.JSON_OPTIONS) ?? throw new InvalidOperationException("The workspace settings could not be read.");
}