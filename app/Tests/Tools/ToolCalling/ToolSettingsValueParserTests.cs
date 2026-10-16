using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the shared check of a stored choice against the option source it was picked from.
/// </summary>
/// <remarks>
/// A stored value can predate the list it was picked from, or come from an organization's
/// configuration with a typo in it. Every tool with a choice relies on this check to report such
/// a value instead of acting on it, so it is tested here once rather than through each tool.
/// </remarks>
[TestFixture]
public sealed class ToolSettingsValueParserTests
{
    private const string KEY = "defaultSafeSearch";

    private const string ERROR_FORMAT = "The setting '{0}' holds '{1}'.";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AnEmptyValuePasses(string? value)
    {
        var settingsValues = value is null ? new Dictionary<string, string>() : new Dictionary<string, string> { [KEY] = value };
        var isValid = ToolSettingsValueParser.TryValidateOptionValue(settingsValues, KEY, ToolSettingsOptionSources.SAFE_SEARCH, ERROR_FORMAT, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True, "Whether a field may stay empty is for the schema's required list to decide, not for this check.");
            Assert.That(error, Is.Empty);
        });
    }

    [Test]
    public void AnOfferedValuePasses()
    {
        var settingsValues = new Dictionary<string, string> { [KEY] = nameof(SafeSearchPolicy.MODERATE) };
        var isValid = ToolSettingsValueParser.TryValidateOptionValue(settingsValues, KEY, ToolSettingsOptionSources.SAFE_SEARCH, ERROR_FORMAT, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Empty);
        });
    }

    [TestCase("moderate")]
    [TestCase("MEDIUM")]
    public void AValueNotOfferedIsReportedWithKeyAndValue(string value)
    {
        var settingsValues = new Dictionary<string, string> { [KEY] = value };
        var isValid = ToolSettingsValueParser.TryValidateOptionValue(settingsValues, KEY, ToolSettingsOptionSources.SAFE_SEARCH, ERROR_FORMAT, out var error);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.False, "The stored value is compared exactly, the way the option source offers it.");
            Assert.That(error, Is.EqualTo($"The setting '{KEY}' holds '{value}'."), "The message has to name the field and the value, or nobody can find what to correct.");
        });
    }
}