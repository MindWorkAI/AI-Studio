using System.Text.Json;

using AIStudio.Assistants.Builder;
using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Checks that JSON which a model or a server wrote may name a field only once.
/// </summary>
/// <remarks>
/// JSON allows a name twice, and readers disagree on which of the values counts. Whatever AI Studio
/// checks in such an answer, another reader may see the other value, so these options refuse it. The
/// stored data, such as the settings, stays out of this: AI Studio wrote it itself, and refusing it
/// would lose it.
/// </remarks>
[TestFixture]
public sealed class DuplicatePropertyTests
{
    private static IEnumerable<TestCaseData> OptionsForAnswersFromOutside()
    {
        yield return new TestCaseData(LuaResponse.JSON_OPTIONS).SetName("The plugins the Assistant Builder writes");
        yield return new TestCaseData(LauncherTextsResponse.JSON_OPTIONS).SetName("The texts the Assistant Builder writes for a chat launcher");
        yield return new TestCaseData(VisualBriefingJson.Canonical).SetName("The answers of the model for a visual briefing");
    }

    [TestCaseSource(nameof(OptionsForAnswersFromOutside))]
    public void TheOptionsRefuseAFieldNamedTwice(JsonSerializerOptions options) => Assert.That(options.AllowDuplicateProperties, Is.False);

    [Test]
    public void APluginWhoseLuaCodeIsNamedTwiceIsRefused()
    {
        var isParsed = LuaResponse.TryParse("""{"full_lua":"ID = \"a\"","full_lua":"ID = \"b\""}""", out _, out var error, out var technicalDetails);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isParsed, Is.False);
            Assert.That(error, Is.EqualTo(LuaResponseParseError.INVALID_JSON));
            Assert.That(technicalDetails, Does.Contain("'full_lua'"), "The user learns which field the model wrote twice.");
        }
    }

    [Test]
    public void TextsForAChatLauncherWithATitleNamedTwiceAreRefused()
    {
        var isParsed = LauncherTextsResponse.TryParse("""{"title":"Budget","title":"Travel"}""", out _, out var error, out var technicalDetails);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isParsed, Is.False);
            Assert.That(error, Is.EqualTo(LuaResponseParseError.INVALID_JSON));
            Assert.That(technicalDetails, Does.Contain("'title'"), "The user learns which field the model wrote twice.");
        }
    }
}