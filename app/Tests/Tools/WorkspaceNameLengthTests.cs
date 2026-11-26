using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the longest name the user may give a workspace, see WorkspaceBehaviour.MAX_WORKSPACE_NAME_LENGTH.
/// </summary>
[TestFixture]
public sealed class WorkspaceNameLengthTests
{
    [Test]
    public void ANameUpToTheLimitFits()
    {
        var name = new string('a', WorkspaceBehaviour.MAX_WORKSPACE_NAME_LENGTH);
        Assert.That(WorkspaceBehaviour.CheckWorkspaceNameLength(name), Is.Null);
    }

    [Test]
    public void ALongerNameIsRefused()
    {
        var name = new string('a', WorkspaceBehaviour.MAX_WORKSPACE_NAME_LENGTH + 1);
        Assert.That(WorkspaceBehaviour.CheckWorkspaceNameLength(name), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TheSpacesAroundANameDoNotCount()
    {
        var name = $"  {new string('a', WorkspaceBehaviour.MAX_WORKSPACE_NAME_LENGTH)}  ";
        Assert.That(WorkspaceBehaviour.CheckWorkspaceNameLength(name), Is.Null);
    }

    [Test]
    public void NoNameLeavesTheCheckToTheEmptyName()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(WorkspaceBehaviour.CheckWorkspaceNameLength(null), Is.Null);
            Assert.That(WorkspaceBehaviour.CheckWorkspaceNameLength(string.Empty), Is.Null);
        }
    }
}