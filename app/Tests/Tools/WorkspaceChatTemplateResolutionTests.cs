using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how a chat finds the workspace its chat template names.
/// </summary>
/// <remarks>
/// A chat template names its workspace by name, because a workspace has an ID of its own on every
/// computer. The chat page and assistant chat launchers both go through the same method, so it has
/// to find the workspace the user sees in the list under that name, create it only when it is
/// missing, and never stop a chat over a name which names nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceChatTemplateResolutionTests
{
    private const string CHAT_TEMPLATE_NAME = "Mail";

    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public async Task PrepareDataDirectory()
    {
        //
        // The data directory and the workspace cache are static state of the whole application,
        // which is why this fixture does not run alongside others, see WorkspaceTreeCacheTests:
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-workspace-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;

        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;

        // Whoever reads the cache next must not get the workspaces of this test:
        WorkspaceBehaviour.InvalidateWorkspaceTreeCache();

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task AnExistingWorkspaceIsFoundByItsNameWithoutSpacesInAnyCase()
    {
        var created = await WorkspaceBehaviour.TryCreateWorkspaceAsync("Mails");
        Assert.That(created.Success, Is.True);

        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateChatTemplateWorkspaceAsync("  mails ", CHAT_TEMPLATE_NAME);
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();

        Assert.Multiple(() =>
        {
            Assert.That(workspaceId, Is.EqualTo(created.Workspace.WorkspaceId), "The template names the workspace the user sees in the list under that name.");
            Assert.That(snapshot.Workspaces, Has.Count.EqualTo(1), "A second workspace of the same name would split the chats of the template.");
        });
    }

    [Test]
    public async Task AMissingWorkspaceIsCreated()
    {
        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateChatTemplateWorkspaceAsync("Contracts", CHAT_TEMPLATE_NAME);
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();

        Assert.Multiple(() =>
        {
            Assert.That(workspaceId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(snapshot.Workspaces.Single(workspace => workspace.WorkspaceId == workspaceId).Name, Is.EqualTo("Contracts"));
        });
    }

    [Test]
    public async Task ANameWhichNamesNothingGivesNoWorkspace()
    {
        var workspaceId = await WorkspaceBehaviour.ResolveOrCreateChatTemplateWorkspaceAsync("   ", CHAT_TEMPLATE_NAME);
        var snapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();

        Assert.Multiple(() =>
        {
            Assert.That(workspaceId, Is.EqualTo(Guid.Empty), "The chat then starts without a workspace, as it would without the template.");
            Assert.That(snapshot.Workspaces, Is.Empty, "A workspace without a name would only show up as an unnamed one in the list.");
        });
    }
}