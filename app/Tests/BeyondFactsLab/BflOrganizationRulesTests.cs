using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which chats of the BeyondFacts Lab receive the guidelines of an organization, and how.
/// </summary>
[TestFixture]
public sealed class BflOrganizationRulesTests
{
    private const string RULES = "- Vor jeder öffentlichen Veranstaltung die Pressestelle kontaktieren.";

    [Test]
    public void OnlyTheChatsOfTheStepsReceiveTheGuidelines()
    {
        var settings = new DataBeyondFactsLab { OrganizationRules = RULES };

        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All)
                Assert.That(BflOrganizationRules.For(new BflStepThreadLocation(Guid.NewGuid(), step), settings), Is.EqualTo(RULES), step.ToString());

            Assert.That(BflOrganizationRules.For(BflOnboardingThreadLocation.INSTANCE, settings), Is.Empty, "The conversation about whether the lab fits a task prepares no appearance.");
        }
    }

    [Test]
    public void WithoutGuidelinesNothingGoesAlong()
    {
        var thread = ThreadWith("Ich spreche auf einem Podium.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflOrganizationRules.Frame(string.Empty), Is.Empty);
            Assert.That(BflOrganizationRules.Frame("  \n"), Is.Empty);
            Assert.That(BflOrganizationRules.WithRules(thread, string.Empty), Is.SameAs(thread));
        }
    }

    [Test]
    public void AChatWithoutAMessageStaysAsItIs()
    {
        var thread = new ChatThread { Blocks = [] };

        Assert.That(BflOrganizationRules.WithRules(thread, RULES), Is.SameAs(thread));
    }

    [Test]
    public void TheRequestGetsACopyOfTheFirstMessageOnly()
    {
        var thread = ThreadWith("Ich spreche auf einem Podium.");
        var answer = thread.Blocks[1];

        var request = BflOrganizationRules.WithRules(thread, RULES);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TextOf(request.Blocks[0]), Is.EqualTo($"<hinweise_der_organisation>\n{RULES}\n</hinweise_der_organisation>\n\nIch spreche auf einem Podium."));
            Assert.That(request.Blocks[1], Is.SameAs(answer), "The answer stays the same object.");
            Assert.That(TextOf(thread.Blocks[0]), Is.EqualTo("Ich spreche auf einem Podium."), "The chat keeps the message as it was.");
        }
    }

    [Test]
    public void OnlyTheOpenPointsEndWithTheGuidelines()
    {
        const string TEXT = "# Offene Punkte\n\n## Recherchieren\n\n- [ ] Studienlage prüfen\n";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflOrganizationRules.AddTo(BflResultKind.OPEN_POINTS, TEXT, RULES, "Vorgaben Ihrer Organisation"), Is.EqualTo($"# Offene Punkte\n\n## Recherchieren\n\n- [ ] Studienlage prüfen\n\n## Vorgaben Ihrer Organisation\n\n{RULES}"));
            Assert.That(BflOrganizationRules.AddTo(BflResultKind.OPEN_POINTS, TEXT, string.Empty, "Vorgaben Ihrer Organisation"), Is.EqualTo(TEXT));

            foreach (var kind in BflResultKind.All.Where(kind => kind is not BflResultKind.OPEN_POINTS))
                Assert.That(BflOrganizationRules.AddTo(kind, TEXT, RULES, "Vorgaben Ihrer Organisation"), Is.EqualTo(TEXT), kind.ToString());
        }
    }

    private static string TextOf(ContentBlock block) => ((ContentText)block.Content!).Text;

    private static ChatThread ThreadWith(string message) => new()
    {
        Blocks =
        [
            new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.USER, Content = new ContentText { Text = message } },
            new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.AI, Content = new ContentText { Text = "Worum geht es Ihnen?" } }
        ]
    };
}