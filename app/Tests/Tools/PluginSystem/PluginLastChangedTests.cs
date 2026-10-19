using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how a plugin states when it was last changed.
/// </summary>
/// <remarks>
/// LAST_CHANGED is optional and always a point in time in UTC, so that the UI can show it in the time
/// zone of the user. A date alone cannot be converted: whether a day in UTC is the same day locally
/// depends on the time. A plugin without the field, or with a value of another form, still loads, and
/// the value then counts as absent. AI Studio writes the field itself whenever it writes a direct chat
/// launcher, and what it writes has to load back to the same point in time.
/// </remarks>
[TestFixture]
public sealed class PluginLastChangedTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("3f6a2c8e-9b1d-4e7a-8c5f-2d4b6e8a0c1f");

    private static readonly DateTimeOffset LAST_CHANGED = new(2026, 10, 12, 14, 30, 5, TimeSpan.Zero);

    private const string LAST_CHANGED_LINE = "LAST_CHANGED = \"2026-10-12T14:30:05Z\"";

    [Test]
    public async Task APointInTimeInUtcIsRead()
    {
        using var plugin = await LoadModelPlugin(LAST_CHANGED_LINE);

        Assert.Multiple(() =>
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.LastChanged, Is.EqualTo(LAST_CHANGED));
            Assert.That(plugin.LastChanged?.Offset, Is.EqualTo(TimeSpan.Zero), "The UI converts from UTC, so the value has to be in UTC.");
        });
    }

    [Test]
    public async Task AMissingFieldIsNoIssue()
    {
        using var plugin = await LoadModelPlugin(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.LastChanged, Is.Null);
        });
    }

    [TestCase("\"2026-10-12\"")]
    [TestCase("\"2026-10-12T14:30:05\"")]
    [TestCase("\"2026-10-12T16:30:05+02:00\"")]
    [TestCase("\"2026-13-01T00:00:00Z\"")]
    [TestCase("\"12.10.2026 14:30:05\"")]
    [TestCase("20261012")]
    public async Task AMalformedValueIsIgnoredAndThePluginStillLoads(string luaValue)
    {
        using var plugin = await LoadModelPlugin($"LAST_CHANGED = {luaValue}");

        Assert.Multiple(() =>
        {
            Assert.That(plugin.IsValid, Is.True, "A malformed LAST_CHANGED is no reason to refuse an otherwise valid plugin.");
            Assert.That(plugin.LastChanged, Is.Null);
        });
    }

    [Test]
    public void ALocalTimeIsWrittenInUtc()
    {
        var localTime = new DateTimeOffset(2026, 10, 12, 16, 30, 5, TimeSpan.FromHours(2));

        Assert.That(PluginBase.FormatLastChanged(localTime), Is.EqualTo("2026-10-12T14:30:05Z"), "The format ends in a literal Z, so a local time must not be written as it is.");
    }

    [Test]
    public void TheWriterLeavesTheFieldOutWithoutAPointInTime()
    {
        var lua = DirectChatLauncherLuaWriter.Write(CreateLauncherMetadata(null), CreateLauncherDefinition());

        Assert.That(lua, Does.Not.Contain("LAST_CHANGED"));
    }

    [Test]
    public async Task WhatTheWriterWritesLoadsBackToTheSamePointInTime()
    {
        var lua = DirectChatLauncherLuaWriter.Write(CreateLauncherMetadata(LAST_CHANGED), CreateLauncherDefinition());
        using var plugin = await PluginFactory.Load(null, lua);

        Assert.Multiple(() =>
        {
            Assert.That(lua, Does.Contain(LAST_CHANGED_LINE));
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.LastChanged, Is.EqualTo(LAST_CHANGED));
        });
    }

    /// <summary>
    /// Loads a model plugin, the plugin type with the fewest fields beyond the ones every plugin has.
    /// </summary>
    /// <param name="lastChangedLine">The Lua line which states LAST_CHANGED, or an empty string.</param>
    private static Task<PluginBase> LoadModelPlugin(string lastChangedLine) => PluginFactory.Load(null, $$"""
        ID = "{{PLUGIN_ID}}"
        NAME = "Models for the tests"
        DESCRIPTION = "A model plugin which declares no models."
        VERSION = "1.0.0"
        {{lastChangedLine}}
        TYPE = "MODEL"
        AUTHORS = {"MindWork AI"}
        SUPPORT_CONTACT = "MindWork AI"
        SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
        CATEGORIES = {"CORE"}
        TARGET_GROUPS = {"EVERYONE"}
        IS_MAINTAINED = true
        DEPRECATION_MESSAGE = ""
        MODELS = {}
        """);

    private static DirectChatLauncherPluginMetadata CreateLauncherMetadata(DateTimeOffset? lastChanged) => new(PLUGIN_ID, "1.0.0", lastChanged, ["MindWork AI"], "MindWork AI",
        "https://github.com/MindWorkAI/AI-Studio", [PluginCategory.CORE], [PluginTargetGroup.EVERYONE], IsMaintained: true, DeprecationMessage: string.Empty, IsAssistantBuilderGenerated: false);

    private static DirectChatLauncherDefinition CreateLauncherDefinition() => new("Launcher for the tests", "Launcher for the tests", "Opens a disappearing chat.", new AssistantChatLaunchConfiguration(string.Empty, null, null, null, null, null));
}