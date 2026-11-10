using AIStudio.Components.Settings;
using AIStudio.Settings;

using MudBlazor;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the paths of the settings window and which pane the window shows for them.
/// </summary>
/// <remarks>
/// Every place which offers settings opens the window at a path. A path which led to no pane, or to a
/// pane the window cannot show, would open an empty window, and nobody would notice until a user did.
/// </remarks>
[TestFixture]
public sealed class SettingsPanesTests
{
    [Test]
    public void EveryPaneHasAPathOfItsOwn()
    {
        var duplicates = SettingsPanes.ALL.GroupBy(pane => pane.Path).Where(group => group.Count() > 1).Select(group => group.Key.Value);
        Assert.That(duplicates, Is.Empty, "Two panes of the settings window share a path.");
    }

    [Test]
    public void EveryPaneShowsASettingsPanel()
    {
        var wrong = SettingsPanes.ALL.Where(pane => !pane.Component.IsSubclassOf(typeof(SettingsPanelBase))).Select(pane => pane.Path.Value);
        Assert.That(wrong, Is.Empty, "The window passes the parameters of SettingsPanelBase to every pane, so its component must derive from it.");
    }

    [Test]
    public void NoPaneLooksLikeItReportsAState()
    {
        Color[] calmColors = [Color.Primary, Color.Secondary, Color.Tertiary, Color.Info, Color.Dark];
        var wrong = SettingsPanes.ALL.Where(pane => !calmColors.Contains(pane.IconColor)).Select(pane => pane.Path.Value);
        Assert.That(wrong, Is.Empty, "Red, orange, and green stand for errors, warnings, and success in the app; the avatar of a pane takes another theme color.");
    }

    [Test]
    public void EveryDeeperPaneHasAParentPane()
    {
        var orphans = SettingsPanes.ALL
            .Where(pane => pane.Path.Parent is { } parent && SettingsPanes.Find(parent) is null)
            .Select(pane => pane.Path.Value);

        Assert.That(orphans, Is.Empty, "A pane below the first level needs a pane at the path above it, where the back button of the window leads.");
    }

    [Test]
    public void APathOpensItsOwnPane()
    {
        var pane = SettingsPanes.Resolve(SettingsPath.WORKSPACES, _ => true);
        Assert.That(pane.Path, Is.EqualTo(SettingsPath.WORKSPACES));
    }

    [Test]
    public void APathWhichLeadsNowhereOpensThePaneAboveIt()
    {
        var pane = SettingsPanes.Resolve(SettingsPath.WORKSPACES.Append("unknown"), _ => true);
        Assert.That(pane.Path, Is.EqualTo(SettingsPath.WORKSPACES));
    }

    [Test]
    public void AnUnavailablePaneOpensTheFirstPane()
    {
        var pane = SettingsPanes.Resolve(SettingsPath.WORKSPACES, candidate => candidate.Path != SettingsPath.WORKSPACES);
        Assert.That(pane, Is.EqualTo(SettingsPanes.ALL[0]));
    }

    [Test]
    public void TheTrailOfAPaneOfTheFirstLevelHoldsOnlyItself()
    {
        var pane = SettingsPanes.Find(SettingsPath.CHAT)!;
        Assert.That(SettingsPanes.Trail(pane), Is.EqualTo(new[] { pane }));
    }

    [Test]
    public void APathOfTheFirstLevelHasNoParent()
    {
        Assert.That(SettingsPath.CHAT.Parent, Is.Null);
    }

    [Test]
    public void AnAppendedSegmentGoesOneLevelDeeper()
    {
        var path = SettingsPath.WORKSPACES.Append("abc");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path.Value, Is.EqualTo("workspaces/abc"));
            Assert.That(path.Parent, Is.EqualTo(SettingsPath.WORKSPACES));
            Assert.That(path.SelfAndAncestors(), Is.EqualTo(new[] { path, SettingsPath.WORKSPACES }));
        }
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("a/b")]
    public void ASegmentMustBeOneNonEmptyLevel(string segment)
    {
        Assert.That(() => SettingsPath.CHAT.Append(segment), Throws.ArgumentException);
    }

    [Test]
    public void TheDefaultPathIsEmpty()
    {
        var path = default(SettingsPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path.Value, Is.Empty);
            Assert.That(path.Parent, Is.Null);
        }
    }
}