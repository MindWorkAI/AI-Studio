using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how Read Web Page stores and reads its Brave Mode.
/// </summary>
/// <remarks>
/// The mode is stored by the name of its enum member and offered through an option source. The
/// two must list the same modes, or the dropdown offers a value the tool cannot read, or the tool
/// knows a mode nobody can pick. And an unset mode has to read as the careful one.
/// </remarks>
[TestFixture]
public sealed class ReadWebPageBraveModeTests
{
    [Test]
    public void TheOptionSourceIsKnown()
    {
        Assert.That(ToolSettingsOptionSources.IsKnown(ToolSettingsOptionSources.BRAVE_MODE), Is.True, "The registry refuses a definition that points at an unknown option source, which would take Read Web Page away entirely.");
    }

    [Test]
    public void TheOptionSourceOffersExactlyTheModes()
    {
        var offeredValues = ToolSettingsOptionSources.Resolve(ToolSettingsOptionSources.BRAVE_MODE).Select(option => option.Value);
        Assert.That(offeredValues, Is.EqualTo(Enum.GetNames<BraveMode>()));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AnUnsetModeReadsAsOff(string? configuredValue)
    {
        Assert.That(ReadWebPageTool.ReadBraveMode(configuredValue), Is.EqualTo(BraveMode.OFF));
    }

    [TestCase(nameof(BraveMode.OFF), BraveMode.OFF)]
    [TestCase(nameof(BraveMode.ON), BraveMode.ON)]
    public void AStoredModeIsReadByItsName(string configuredValue, BraveMode expected)
    {
        Assert.That(ReadWebPageTool.ReadBraveMode(configuredValue), Is.EqualTo(expected));
    }

    [TestCase("1")]
    [TestCase("ON, OFF")]
    public void ANumberOrSeveralNamesReadAsOff(string configuredValue)
    {
        Assert.That(ReadWebPageTool.ReadBraveMode(configuredValue), Is.EqualTo(BraveMode.OFF), "Enum.TryParse would read both as ON, a mode nobody wrote.");
    }
}