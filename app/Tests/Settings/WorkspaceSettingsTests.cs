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

    [TestCase(ChatStorageChoice.DO_NOT_STORE)]
    [TestCase(ChatStorageChoice.STORE_ON_SAVE)]
    [TestCase(ChatStorageChoice.STORE_TEMPORARILY)]
    [TestCase(ChatStorageChoice.STORE_INDEFINITELY)]
    public void EveryChoiceReadsBackAsChosen(ChatStorageChoice choice)
    {
        var workspace = new DataWorkspace();
        workspace.SetChoice(isInWorkspace: false, choice);
        workspace.SetChoice(isInWorkspace: true, choice);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.GetChoice(isInWorkspace: false), Is.EqualTo(choice));
            Assert.That(workspace.GetChoice(isInWorkspace: true), Is.EqualTo(choice));
        }
    }

    [Test]
    public void ASettingsFileWithoutAChoiceForWorkspacesKeepsTheirChats()
    {
        var workspace = Read("""{ "StorageBehavior": "STORE_CHATS_AUTOMATICALLY", "StorageTemporaryMaintenancePolicy": "DELETE_OLDER_THAN_30_DAYS" }""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.GetChoice(isInWorkspace: false), Is.EqualTo(ChatStorageChoice.STORE_TEMPORARILY));
            Assert.That(workspace.GetChoice(isInWorkspace: true), Is.EqualTo(ChatStorageChoice.STORE_INDEFINITELY), "Chats in workspaces were never deleted before they had a choice of their own.");
            Assert.That(workspace.GetDeletionPolicy(isInWorkspace: true), Is.EqualTo(WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE));
        }
    }

    [Test]
    public void ChoosingForAllChatsLeavesTheWorkspacesAsTheyWere()
    {
        var workspace = new DataWorkspace();
        var choiceOfWorkspaces = workspace.GetChoice(isInWorkspace: true);

        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.DO_NOT_STORE);

        Assert.That(workspace.GetChoice(isInWorkspace: true), Is.EqualTo(choiceOfWorkspaces), "Whoever changes how chats are kept in general does not mean to change the workspaces as well.");
    }

    [Test]
    public void SameAsForAllChatsTakesOverWhatTheyGetAtThatMoment()
    {
        var workspace = new DataWorkspace();
        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.STORE_TEMPORARILY);
        workspace.SetDuration(isInWorkspace: false, WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS);

        workspace.SetChoice(isInWorkspace: true, ChatStorageChoice.SAME_AS_ALL_CHATS);
        var choiceTakenOver = workspace.GetChoice(isInWorkspace: true);
        var durationTakenOver = workspace.GetDuration(isInWorkspace: true);

        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.DO_NOT_STORE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choiceTakenOver, Is.EqualTo(ChatStorageChoice.STORE_TEMPORARILY));
            Assert.That(durationTakenOver, Is.EqualTo(WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS));
            Assert.That(workspace.GetChoice(isInWorkspace: true), Is.EqualTo(ChatStorageChoice.STORE_TEMPORARILY), "The same as for all chats is a starting point, not a link.");
        }
    }

    [Test]
    public void ChatsSavedByHandAreNeverDeleted()
    {
        var workspace = new DataWorkspace
        {
            StorageBehavior = WorkspaceStorageBehavior.STORE_CHATS_MANUALLY,
            StorageTemporaryMaintenancePolicy = WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workspace.GetChoice(isInWorkspace: false), Is.EqualTo(ChatStorageChoice.STORE_ON_SAVE));
            Assert.That(workspace.GetDeletionPolicy(isInWorkspace: false), Is.EqualTo(WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE), "A chat the user saved on purpose stays until they delete it.");
        }
    }

    [Test]
    public void StoringTemporarilyAgainKeepsTheDurationChosenBefore()
    {
        var workspace = new DataWorkspace();
        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.STORE_TEMPORARILY);
        workspace.SetDuration(isInWorkspace: false, WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS);

        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.STORE_ON_SAVE);
        workspace.SetChoice(isInWorkspace: false, ChatStorageChoice.STORE_TEMPORARILY);

        Assert.That(workspace.GetDuration(isInWorkspace: false), Is.EqualTo(WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS));
    }

    [Test]
    public void TheChoiceForWorkspacesSurvivesStoring()
    {
        var workspace = new DataWorkspace();
        workspace.SetChoice(isInWorkspace: true, ChatStorageChoice.STORE_TEMPORARILY);
        workspace.SetDuration(isInWorkspace: true, WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS);

        var stored = Read(JsonSerializer.Serialize(workspace, SettingsManager.JSON_OPTIONS));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.WorkspaceChatsStorageBehavior, Is.EqualTo(WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY));
            Assert.That(stored.WorkspaceChatsMaintenancePolicy, Is.EqualTo(WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS));
        }
    }

    [Test]
    public void AChoiceNotMadeYetStaysOne()
    {
        var stored = Read(JsonSerializer.Serialize(new DataWorkspace(), SettingsManager.JSON_OPTIONS));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.WorkspaceChatsStorageBehavior, Is.Null);
            Assert.That(stored.WorkspaceChatsMaintenancePolicy, Is.Null);
        }
    }

    [Test]
    public void AnUnreadableChoiceCountsAsNotMade()
    {
        var workspace = Read("""{ "WorkspaceChatsStorageBehavior": "SOMETHING_NEWER" }""");
        Assert.That(workspace.WorkspaceChatsStorageBehavior, Is.Null, "A value nobody chose must not turn into the first choice of the list.");
    }

    [Test]
    public void TheWholeSettingsFileCanBeRead()
    {
        var settings = JsonSerializer.Deserialize<Data>("""{ "Workspace": { "WorkspaceChatsStorageBehavior": "STORE_CHATS_MANUALLY" } }""", SettingsManager.JSON_OPTIONS);
        Assert.That(settings?.Workspace.WorkspaceChatsStorageBehavior, Is.EqualTo(WorkspaceStorageBehavior.STORE_CHATS_MANUALLY), "Every type of the settings needs a converter which can read it, or no settings file can be read at all.");
    }

    private static DataWorkspace Read(string json) => JsonSerializer.Deserialize<DataWorkspace>(json, SettingsManager.JSON_OPTIONS) ?? throw new InvalidOperationException("The workspace settings could not be read.");
}