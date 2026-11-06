using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that the system prompt reports the parts it is made of.
/// </summary>
/// <remarks>
/// The dialog which shows the system prompt names its parts from what building it decided. The text
/// itself is what the request sends and the token count measures, so recording the parts must never
/// change it: each part stands in the text exactly as recorded, in the order recorded.
/// </remarks>
[TestFixture]
public sealed class ChatThreadSystemPromptTests
{
    private const string BASE_PROMPT = "You are a helpful assistant.";
    private const string TEMPLATE_PROMPT = "You review contracts and point out risks.";
    private const string RETRIEVED_DATA = "The following useful information will help you in processing the user prompt: the travel policy allows trains only.";

    private static readonly string TEMPLATE_ID = Guid.NewGuid().ToString();
    private static readonly string PROFILE_ID = Guid.NewGuid().ToString();

    [Test]
    public void TheBasePromptStandsAloneWithoutAnythingElse()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: true);
        var prepared = Thread().BuildSystemPrompt(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prepared.Text, Is.EqualTo(BASE_PROMPT));
            Assert.That(prepared.Parts.Select(part => part.Kind), Is.EqualTo(new[] { SystemPromptPartKind.BASE_PROMPT }));
            Assert.That(prepared.SwitchedOffProfile, Is.EqualTo(Profile.NO_PROFILE));
        }
    }

    [Test]
    public void EveryPartStandsInTheTextInItsOrder()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: true);
        var thread = Thread();
        thread.IncludeDateTime = true;
        thread.SelectedChatTemplate = TEMPLATE_ID;
        thread.SelectedProfile = PROFILE_ID;
        thread.AugmentedData = RETRIEVED_DATA;

        var tool = Tool("calculate", "Use `calculate` for every arithmetic.");
        var prepared = thread.BuildSystemPrompt(settings, [tool]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prepared.Parts.Select(part => part.Kind), Is.EqualTo(new[]
            {
                SystemPromptPartKind.DATE_TIME,
                SystemPromptPartKind.CHAT_TEMPLATE,
                SystemPromptPartKind.RETRIEVED_DATA,
                SystemPromptPartKind.PROFILE,
                SystemPromptPartKind.TOOL_INSTRUCTIONS,
            }));

            var position = 0;
            foreach (var part in prepared.Parts)
            {
                var found = prepared.Text.IndexOf(part.Text, position, StringComparison.Ordinal);
                Assert.That(found, Is.GreaterThanOrEqualTo(position), $"The part {part.Kind} must stand in the text after the parts before it.");
                position = found + part.Text.Length;
            }

            Assert.That(position, Is.EqualTo(prepared.Text.Length), "Nothing may follow the last part.");
            Assert.That(prepared.Parts[1].SourceName, Is.EqualTo("Legal review"));
            Assert.That(prepared.Parts[1].Text, Is.EqualTo(TEMPLATE_PROMPT));
            Assert.That(prepared.Parts[2].Text, Is.EqualTo(RETRIEVED_DATA));
            Assert.That(prepared.Parts[3].SourceName, Is.EqualTo("Developer"));
            Assert.That(prepared.Parts[4].Tools, Is.EqualTo(new[] { tool }));
            Assert.That(prepared.BasePrompt, Is.EqualTo(TEMPLATE_PROMPT));
        }
    }

    [Test]
    public void AChatTemplateWhichForbidsProfilesSwitchesTheSelectedProfileOff()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: false);
        var thread = Thread();
        thread.SelectedChatTemplate = TEMPLATE_ID;
        thread.SelectedProfile = PROFILE_ID;

        var prepared = thread.BuildSystemPrompt(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prepared.Text, Is.EqualTo(TEMPLATE_PROMPT));
            Assert.That(prepared.Parts.Select(part => part.Kind), Is.EqualTo(new[] { SystemPromptPartKind.CHAT_TEMPLATE }));
            Assert.That(prepared.SwitchedOffProfile.Name, Is.EqualTo("Developer"));
        }
    }

    [Test]
    public void AChatTemplateWhichForbidsProfilesSwitchesNothingOffWithoutAProfile()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: false);
        var thread = Thread();
        thread.SelectedChatTemplate = TEMPLATE_ID;
        thread.SelectedProfile = Profile.NO_PROFILE.Id;

        var prepared = thread.BuildSystemPrompt(settings);
        Assert.That(prepared.SwitchedOffProfile, Is.EqualTo(Profile.NO_PROFILE), "Nothing was selected, so the chat template switched nothing off.");
    }

    [Test]
    public void AChatTemplateWhichNoLongerExistsLeavesTheBasePrompt()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: true);
        var thread = Thread();
        thread.SelectedChatTemplate = Guid.NewGuid().ToString();

        var prepared = thread.BuildSystemPrompt(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prepared.Text, Is.EqualTo(BASE_PROMPT));
            Assert.That(prepared.Parts.Select(part => part.Kind), Is.EqualTo(new[] { SystemPromptPartKind.BASE_PROMPT }));
        }
    }

    [Test]
    public void AToolWithoutInstructionsAddsNoPart()
    {
        using var rustService = new RustService("1", "unused");
        var settings = Settings(rustService, allowProfileUsage: true);
        var withInstructions = Tool("calculate", "Use `calculate` for every arithmetic.");
        var withoutInstructions = Tool("silent_tool", "   ");

        using (Assert.EnterMultipleScope())
        {
            var silent = Thread().BuildSystemPrompt(settings, [withoutInstructions]);
            Assert.That(silent.Text, Is.EqualTo(BASE_PROMPT));
            Assert.That(silent.Parts.Select(part => part.Kind), Is.EqualTo(new[] { SystemPromptPartKind.BASE_PROMPT }));

            var mixed = Thread().BuildSystemPrompt(settings, [withoutInstructions, withInstructions]);
            Assert.That(mixed.Parts[^1].Tools, Is.EqualTo(new[] { withInstructions }), "Only a tool with instructions is a source of that part.");
            Assert.That(mixed.Text, Does.Not.Contain("`silent_tool`"));
        }
    }

    private static ChatThread Thread() => new() { SystemPrompt = BASE_PROMPT };

    private static ToolDefinition Tool(string name, string instructions) => new()
    {
        Id = name,
        ImplementationKey = name,
        SystemPromptInstructions = instructions,
        Function = new() { Name = name },
    };

    private static SettingsManager Settings(RustService rustService, bool allowProfileUsage)
    {
        var settings = new SettingsManager(NullLogger<SettingsManager>.Instance, rustService);
        settings.ConfigurationData.ChatTemplates.Add(new ChatTemplate
        {
            Num = 1,
            Id = TEMPLATE_ID,
            Name = "Legal review",
            SystemPrompt = TEMPLATE_PROMPT,
            AllowProfileUsage = allowProfileUsage,
        });

        settings.ConfigurationData.Profiles.Add(new Profile
        {
            Num = 1,
            Id = PROFILE_ID,
            Name = "Developer",
            NeedToKnow = "I write C# for a living.",
            Actions = "Answer briefly.",
        });

        return settings;
    }
}