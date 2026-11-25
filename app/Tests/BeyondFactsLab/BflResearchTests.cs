using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab reads a research from the answer of an agent and the calls of its tools.
/// </summary>
[TestFixture]
public sealed class BflResearchTests
{
    private static readonly DateTimeOffset CREATED_AT_UTC = new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);

    private static readonly BflResearchDetail[] DETAILS =
    [
        new() { FieldId = BflFieldCatalog.FORMAT, Value = "Podium" },
        new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" },
    ];

    [Test]
    public void AResearchKeepsWhatLeftAIStudio()
    {
        ToolInvocationTrace[] invocations =
        [
            Call(2, ToolSelectionRules.WEB_SEARCH_TOOL_ID, BflResearch.QUERY_ARGUMENT, "Podium Klimapolitik 2026"),
            Call(1, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, BflResearch.URL_ARGUMENT, "https://example.org/event"),
            Call(3, ToolSelectionRules.WEB_SEARCH_TOOL_ID, BflResearch.QUERY_ARGUMENT, "Podium Klimapolitik 2026"),
            Call(4, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, BflResearch.URL_ARGUMENT, "https://example.org/unknown-title"),
            Call(5, ToolSelectionRules.WEB_SEARCH_TOOL_ID, BflResearch.QUERY_ARGUMENT, "Blocked query", wasExecuted: false),
            Call(6, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, BflResearch.URL_ARGUMENT, "not an address"),
            Call(7, "calculate", "expression", "1 + 1"),
        ];

        Source[] sources =
        [
            new("Event page", "https://example.org/event", SourceOrigin.TOOL),
            new("Written by the model", "https://example.org/unknown-title", SourceOrigin.LLM),
        ];

        var research = BflResearch.Of("## Veranstaltung\n\nEin Podium.", invocations, sources, DETAILS, CREATED_AT_UTC);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.Text, Is.EqualTo("## Veranstaltung\n\nEin Podium."));
            Assert.That(research.SearchQueries, Is.EqualTo(new[] { "Podium Klimapolitik 2026" }));
            Assert.That(research.Pages.Select(page => page.Url), Is.EqualTo(new[] { "https://example.org/event", "https://example.org/unknown-title" }));
            Assert.That(research.Pages.Select(page => page.Title), Is.EqualTo(new[] { "Event page", "https://example.org/unknown-title" }));
            Assert.That(research.Details.Select(detail => detail.Value), Is.EqualTo(new[] { "Podium", "Klimapolitik" }));
            Assert.That(research.Details, Has.None.SameAs(DETAILS[0]), "The research keeps a copy of the details.");
            Assert.That(research.CreatedAtUtc, Is.EqualTo(CREATED_AT_UTC));
        }
    }

    [Test]
    public void ACodeBlockAroundTheAnswerIsTakenOff()
    {
        var research = BflResearch.Of("```markdown\n## Debatte\n\nText\n```", [], [], DETAILS, CREATED_AT_UTC);

        Assert.That(research.Text, Is.EqualTo("## Debatte\n\nText"));
    }

    [Test]
    public void AVeryLongAnswerIsShortenedAtTheEndOfALine()
    {
        var line = new string('x', 99);
        var answer = string.Join("\n", Enumerable.Repeat(line, 1_000));

        var research = BflResearch.Of(answer, [], [], DETAILS, CREATED_AT_UTC);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.Text.Length, Is.LessThanOrEqualTo(BflResearch.MAX_TEXT_CHARACTERS));
            Assert.That(research.Text, Does.EndWith(line));
        }
    }

    [Test]
    public void ALongTitleIsShortened()
    {
        var title = new string('T', BflResearch.MAX_TITLE_CHARACTERS + 50);
        var research = BflResearch.Of("Text", [Call(1, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, BflResearch.URL_ARGUMENT, "https://example.org/")], [new(title, "https://example.org/", SourceOrigin.TOOL)], DETAILS, CREATED_AT_UTC);

        Assert.That(research.Pages.Single().Title, Has.Length.EqualTo(BflResearch.MAX_TITLE_CHARACTERS).And.EndWith("…"));
    }

    [Test]
    public void AnAnswerWithoutTextGivesAnEmptyResearch()
    {
        var research = BflResearch.Of("  \n ", [], [], DETAILS, CREATED_AT_UTC);

        Assert.That(research.Text, Is.Empty);
    }

    [Test]
    public void AResearchIsOutdatedOnceThePublicDetailsChanged()
    {
        var research = BflResearch.Of("Text", [], [], DETAILS, CREATED_AT_UTC);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(research.IsOutdated([new() { FieldId = BflFieldCatalog.FORMAT, Value = "Podium" }, new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" }]), Is.False);
            Assert.That(research.IsOutdated([new() { FieldId = BflFieldCatalog.FORMAT, Value = "Podium" }, new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Energiepolitik" }]), Is.True);
            Assert.That(research.IsOutdated([new() { FieldId = BflFieldCatalog.FORMAT, Value = "Podium" }]), Is.True);
            Assert.That(research.IsOutdated([.. DETAILS, new() { FieldId = BflFieldCatalog.EVENT_LINK, Value = "https://example.org/" }]), Is.True);
        }
    }

    private static ToolInvocationTrace Call(int order, string toolId, string argument, string value, bool wasExecuted = true) => new()
    {
        Order = order,
        ToolId = toolId,
        WasExecuted = wasExecuted,
        Status = wasExecuted ? ToolInvocationTraceStatus.SUCCESS : ToolInvocationTraceStatus.BLOCKED,
        Arguments = new() { [argument] = value },
    };
}