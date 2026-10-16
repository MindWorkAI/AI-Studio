using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the notice which says that AI generated an answer the user copies or exports.
/// </summary>
/// <remarks>
/// The notice must leave the answer itself untouched, and it must read as a notice: not as part of the
/// last list item, not inside a code block the answer left open, and not with stray backslashes. Until
/// somebody switches it on, it stays away entirely.
/// </remarks>
[TestFixture]
public sealed class AIGeneratedContentDisclosureServiceTests
{
    private const string NOTICE = @"**This content was generated using artificial intelligence \(AI\)\.**";

    [Test]
    public void TheNoticeIsOffByDefault()
    {
        var settingsManager = CreateSettingsManager();
        var service = new AIGeneratedContentDisclosureService(settingsManager);

        Assert.That(settingsManager.ConfigurationData.App.AddAIGeneratedContentDisclosure, Is.False);
        Assert.That(service.AddDisclosureToMarkdown("The answer."), Is.EqualTo("The answer."), "Without the setting, the answer leaves AI Studio as it is.");
    }

    [Test]
    public void TheNoticeEndsTheAnswerAsAParagraphOfItsOwn()
    {
        var answer = Lines("- first point", "- second point");

        Assert.That(CreateServiceWithNotice().AddDisclosureToMarkdown(answer), Is.EqualTo(Lines(answer, string.Empty, NOTICE)));
    }

    [Test]
    public void TrailingLineBreaksDoNotWidenTheGap()
    {
        var answer = Lines("The answer.", string.Empty, string.Empty);

        Assert.That(CreateServiceWithNotice().AddDisclosureToMarkdown(answer), Is.EqualTo(Lines("The answer.", string.Empty, NOTICE)));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void AnEmptyAnswerStaysEmpty(string answer)
    {
        Assert.That(CreateServiceWithNotice().AddDisclosureToMarkdown(answer), Is.EqualTo(answer), "A notice alone would say that AI wrote something which is not there.");
    }

    [Test]
    public void AnOpenCodeBlockIsClosedBeforeTheNotice()
    {
        var answer = Lines("Here is the code:", string.Empty, "```csharp", "var answer = 42;");

        Assert.That(CreateServiceWithNotice().AddDisclosureToMarkdown(answer), Is.EqualTo(Lines(answer, "```", string.Empty, NOTICE)));
    }

    [Test]
    public void TheNoticeIsShownAsBoldTextBelowTheCode()
    {
        var answer = Lines("```csharp", "var answer = 42;");
        var html = Markdig.Markdown.ToHtml(CreateServiceWithNotice().AddDisclosureToMarkdown(answer), Markdown.SAFE_MARKDOWN_PIPELINE);

        Assert.That(html.TrimEnd(), Does.EndWith("</code></pre>\n<p><strong>This content was generated using artificial intelligence (AI).</strong></p>"), "The escapes keep the parentheses and the period as plain text, and the closed fence keeps the notice out of the code block.");
    }

    private static AIGeneratedContentDisclosureService CreateServiceWithNotice()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.AddAIGeneratedContentDisclosure = true;
        return new AIGeneratedContentDisclosureService(settingsManager);
    }

    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
}
