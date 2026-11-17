using AIStudio.Tools;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;

namespace AIStudio.Tests.Icons;

/// <summary>
/// Checks how the icons an assistant plugin names turn into icons of the app.
/// </summary>
/// <remarks>
/// Assistant plugins name a Lucide icon as "lucide:" followed by its name on lucide.dev, and an icon of MudBlazor
/// by its C# name. Both stay public API: plugins written before Lucide keep their icons.
/// </remarks>
[TestFixture]
public sealed class AssistantPluginIconTests
{
    [Test]
    public void ALucideNameGivesItsGlyph()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssistantComponentPropHelper.GetIcon("lucide:house"), Is.EqualTo(LucideIcons.HOUSE));
            Assert.That(AssistantComponentPropHelper.GetIcon("lucide:trash"), Is.EqualTo(LucideIcons.TRASH));
            Assert.That(AssistantComponentPropHelper.GetIcon("@lucide:house"), Is.EqualTo(LucideIcons.HOUSE));
        }
    }

    [Test]
    public void AnAliasOrDeprecatedLucideNameKeepsWorking()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssistantComponentPropHelper.GetIcon("lucide:home"), Is.EqualTo(LucideIcons.HOUSE));
            Assert.That(AssistantComponentPropHelper.GetIcon("lucide:trash-2"), Is.EqualTo(LucideIcons.TRASH_2));
        }
    }

    [Test]
    public void AMudBlazorNameKeepsItsIcon()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssistantComponentPropHelper.GetIcon("Icons.Material.Filled.Home"), Is.EqualTo(MudBlazor.Icons.Material.Filled.Home));
            Assert.That(AssistantComponentPropHelper.GetIcon("@Icons.Material.Filled.Home"), Is.EqualTo(MudBlazor.Icons.Material.Filled.Home));
        }
    }

    [TestCase("")]
    [TestCase("lucide:")]
    [TestCase("lucide:House")]
    [TestCase("lucide: house")]
    [TestCase("lucide:no-such-icon")]
    [TestCase("Lucide:house")]
    [TestCase("house")]
    [TestCase("Icons.Material.Filled.NoSuchIcon")]
    public void AnUnknownNameGivesNoIcon(string name) => Assert.That(AssistantComponentPropHelper.GetIcon(name), Is.Empty);

    [Test]
    public void TheIconOfASwitchFollowsTheSameNames()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(new AssistantSwitch { Icon = "lucide:house" }.GetIcon(), Is.EqualTo(LucideIcons.HOUSE));
            Assert.That(new AssistantSwitch { Icon = "Icons.Material.Filled.Home" }.GetIcon(), Is.EqualTo(MudBlazor.Icons.Material.Filled.Home));
            Assert.That(new AssistantSwitch { Icon = "lucide:no-such-icon" }.GetIcon(), Is.Empty);
        }
    }
}