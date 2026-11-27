using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;

using Lua;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that assistant plugins keep the former names of props.
/// </summary>
/// <remarks>
/// Before AI Studio v26.11.2, the web content reader called its preselection of the content cleaner
/// <c>PreselectContentCleanerAgent</c>. Plugins keep that name for good, because a plugin which changes
/// it needs a new security audit.
/// </remarks>
[TestFixture]
public sealed class AssistantPropFormerNamesTests
{
    private const string FORMER_NAME = "PreselectContentCleanerAgent";
    private const string CURRENT_NAME = "PreselectContentCleaner";
    private const string READER_NAME = "webContent";

    private static PropSpec WebContentReaderSpec => ComponentPropSpecs.SPECS[AssistantComponentType.WEB_CONTENT_READER];

    [TestCase(FORMER_NAME)]
    [TestCase(CURRENT_NAME)]
    public async Task BothNamesPreselectTheContentCleaner(string propName)
    {
        using var plugin = await LoadAssistantAsync($"""["{propName}"] = true""");
        var reader = FindWebContentReader(plugin);
        var state = new AssistantState();
        reader.InitializeState(state);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reader.PreselectContentCleaner, Is.True);
            Assert.That(state.WebContent[READER_NAME].PreselectContentCleaner, Is.True);
        }
    }

    [Test]
    public async Task TheCurrentNameWinsOverTheFormerName()
    {
        using var plugin = await LoadAssistantAsync($"""["{FORMER_NAME}"] = true, ["{CURRENT_NAME}"] = false""");

        Assert.That(FindWebContentReader(plugin).PreselectContentCleaner, Is.False);
    }

    [Test]
    public void AnUpdateWithTheFormerNameReachesTheCurrentProp()
    {
        var update = new LuaTable { [FORMER_NAME] = true };

        var isApplied = WebContentReaderSpec.TryGetCurrentName(FORMER_NAME, update, out var currentName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isApplied, Is.True);
            Assert.That(currentName, Is.EqualTo(CURRENT_NAME));
        }
    }

    [Test]
    public void AnUpdateWithBothNamesTakesTheCurrentOne()
    {
        var update = new LuaTable { [FORMER_NAME] = true, [CURRENT_NAME] = false };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(WebContentReaderSpec.TryGetCurrentName(FORMER_NAME, update, out _), Is.False, "The former name is ignored next to the current one.");
            Assert.That(WebContentReaderSpec.TryGetCurrentName(CURRENT_NAME, update, out var currentName), Is.True);
            Assert.That(currentName, Is.EqualTo(CURRENT_NAME));
        }
    }

    [Test]
    public void EveryFormerNameLeadsToAKnownProp()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var (type, spec) in ComponentPropSpecs.SPECS)
            {
                foreach (var (formerName, currentName) in spec.FormerNames)
                {
                    Assert.That(spec.Required.Concat(spec.Optional), Does.Contain(currentName), $"The former name '{formerName}' of {type} leads to '{currentName}', which is no prop of it.");
                    Assert.That(spec.Required.Concat(spec.Optional), Does.Not.Contain(formerName), $"The former name '{formerName}' is a prop of {type} again.");
                }
            }
        }
    }

    /// <summary>
    /// Loads an assistant plugin whose form holds a web content reader with the given props besides its name.
    /// </summary>
    private static async Task<PluginAssistants> LoadAssistantAsync(string readerProps)
    {
        var lua = $$"""
            ID = "a9d4e2b7-61c3-4f85-b0e8-3c7f19d5a246"
            NAME = "Assistant for the tests"
            DESCRIPTION = "An assistant plugin with a web content reader."
            VERSION = "1.0.0"
            TYPE = "ASSISTANT"
            AUTHORS = {"MindWork AI"}
            SUPPORT_CONTACT = "MindWork AI"
            SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
            CATEGORIES = {"COMMON"}
            TARGET_GROUPS = {"EVERYONE"}
            IS_MAINTAINED = true
            DEPRECATION_MESSAGE = ""

            ASSISTANT = {
                ["Title"] = "Web page summary",
                ["Description"] = "Summarizes a web page.",
                ["SystemPrompt"] = "You summarize the web page the user provides.",
                ["SubmitText"] = "Summarize",
                ["AllowProfiles"] = false,
                ["UI"] = {
                    ["Type"] = "FORM",
                    ["Children"] = {
                        {
                            ["Type"] = "WEB_CONTENT_READER",
                            ["Props"] = { ["Name"] = "{{READER_NAME}}", {{readerProps}} }
                        }
                    }
                }
            }
            """;

        var plugin = await PluginFactory.Load(null, lua);
        if (plugin is not PluginAssistants { IsValid: true } assistantPlugin)
        {
            var issues = string.Join(" ", plugin.Issues);
            plugin.Dispose();
            throw new InvalidOperationException($"The code of this test did not produce a valid assistant plugin: {issues}");
        }

        return assistantPlugin;
    }

    private static AssistantWebContentReader FindWebContentReader(PluginAssistants plugin) => plugin.RootComponent!.Children.OfType<AssistantWebContentReader>().Single();
}