using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what Search Mails offers, what it accepts, and what a chat has to keep once it searched.
/// </summary>
/// <remarks>
/// The tool belongs to a preview, so it must not show up before the user switched it on. Once it
/// searched, the content of a mailbox is in the chat, and from then on the chat has to keep to what
/// that mailbox demands: no less trusted provider, and data going no further than the mailbox
/// allows. A mailbox must never reach a model another way, through Semantic Search above all.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class SearchMailsToolTests : ToolRegistryTestBase
{
    private static readonly DataSourceMailbox WORK = new()
    {
        Num = 1,
        Id = "9e4b2c7a-1d3f-4a8e-b6c5-2f0d7e1b3a64",
        Name = "Work",
        ConfidenceLevel = ConfidenceLevel.MEDIUM,
        OutboundDataRestriction = OutboundDataRestriction.ONLY_LINKS_FROM_CHAT,
        MaxMatches = 10,
    };

    private static readonly DataSourceMailbox PRIVATE = new()
    {
        Num = 2,
        Id = "4c8d1e6b-3a9f-4b2c-8e7d-5f1a0b9c2d36",
        Name = "Private",
        ConfidenceLevel = ConfidenceLevel.HIGH,
        OutboundDataRestriction = OutboundDataRestriction.ONLY_CONFIGURED_SERVICES,
        MaxMatches = 5,
    };

    [Test]
    public void TheToolExistsOnlyWhileBothPreviewsAreSwitchedOn()
    {
        var tool = this.Tool();
        var withoutPreviews = tool.IsAvailable;

        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_RAG_2024);
        var withLocalRagOnly = tool.IsAvailable;

        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_MAILBOXES_2026);
        var withBoth = tool.IsAvailable;

        Assert.Multiple(() =>
        {
            Assert.That(withoutPreviews, Is.False);
            Assert.That(withLocalRagOnly, Is.False, "Mailboxes are a preview of their own, on top of local RAG.");
            Assert.That(withBoth, Is.True);
        });
    }

    [Test]
    public async Task WithoutAMailboxTheToolAsksForOne()
    {
        var tool = this.Tool();
        var withoutMailbox = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string>());

        this.SettingsManager.ConfigurationData.Mailboxes.Add(WORK);
        var withMailbox = await tool.ValidateConfigurationAsync(tool.GetDefinition(), new Dictionary<string, string>());

        Assert.Multiple(() =>
        {
            Assert.That(withoutMailbox?.IsConfigured, Is.False, "The selection shows the tool as not set up, with what to do about it.");
            Assert.That(withoutMailbox?.Message, Is.Not.Empty);
            Assert.That(withMailbox, Is.Null);
        });
    }

    [Test]
    public async Task TheRegistryTakesTheDefinition()
    {
        var registry = this.CreateRegistry(new TestTool(this.Tool().GetDefinition()));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [ToolSelectionRules.SEARCH_MAILS_TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { ToolSelectionRules.SEARCH_MAILS_TOOL_ID }), "The registry drops a definition it cannot accept, with no more than a warning in the log.");
    }

    [Test]
    public void TheToolKeepsToTheRulesOfAMailbox()
    {
        var tool = this.Tool();

        Assert.Multiple(() =>
        {
            Assert.That(new MailboxToolCollection().GetDefinition().ToolIds, Does.Contain(ToolSelectionRules.SEARCH_MAILS_TOOL_ID), "The mailbox collection states the confidence the tool needs.");
            Assert.That(tool.OutboundData, Is.EqualTo(ToolOutboundData.CONFIGURED_SERVICE), "The query goes to the embedding provider, so a restricted chat may still search.");
            Assert.That(tool.ReturnsUntrustedExternalContent, Is.True, "Mails are written by others.");
        });
    }

    [Test]
    public void TheFunctionOffersExactlyTheMailboxesGiven()
    {
        var function = SearchMailsTool.DescribeMailboxes(this.Tool().GetDefinition().Function, [WORK, PRIVATE]);
        var properties = JsonNode.Parse(function.Parameters.GetRawText())!["properties"]!;

        Assert.Multiple(() =>
        {
            Assert.That(function.DescriptionForLLM, Does.Contain($"- id={WORK.Id}, name='Work', results per page=10, last page=9"));
            Assert.That(function.DescriptionForLLM, Does.Contain($"- id={PRIVATE.Id}, name='Private', results per page=5, last page=19"));
            Assert.That(properties[MailToolArguments.MAILBOX_IDS_ARGUMENT]!["items"]!["enum"]!.AsArray().Select(id => id!.GetValue<string>()), Is.EqualTo(new[] { WORK.Id, PRIVATE.Id }));
            Assert.That(properties["query"], Is.Not.Null);
            Assert.That(properties["page"], Is.Not.Null);
        });
    }

    [Test]
    public void WithoutAQueryTheMailsAreListed()
    {
        var request = SearchMailsTool.ReadRequest(Arguments("""{"is_unread":true}"""), [WORK, PRIVATE], TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(request.Query, Is.Null);
            Assert.That(request.Mailboxes.Select(mailbox => mailbox.Id), Is.EqualTo(new[] { WORK.Id, PRIVATE.Id }));
            Assert.That(request.Conditions.Filter.IsUnread, Is.True);
            Assert.That(request.Page, Is.EqualTo(1));
        });
    }

    [Test]
    public void AnEmptyQueryIsRefusedWithTheWayToListTheMails()
    {
        var message = Refusal(() => SearchMailsTool.ReadRequest(Arguments("""{"query":"  "}"""), [WORK], TimeZoneInfo.Utc));

        Assert.That(message, Does.Contain("'query' must not be empty").And.Contain("Leave it out to list the mails meeting the conditions"));
    }

    [Test]
    public void APageAfterTheFirstNeedsExactlyOneMailbox()
    {
        var message = Refusal(() => SearchMailsTool.ReadRequest(Arguments("""{"page":2}"""), [WORK, PRIVATE], TimeZoneInfo.Utc));
        var request = SearchMailsTool.ReadRequest(Arguments($$"""{"page":2,"mailbox_ids":["{{WORK.Id}}"]}"""), [WORK, PRIVATE], TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("exactly one mailbox"), "The mailboxes have pages of different sizes.");
            Assert.That(request.Page, Is.EqualTo(2));
        });
    }

    [Test]
    public void APageBeyondTheLastIsRefusedWithTheLastPage()
    {
        var message = Refusal(() => SearchMailsTool.ReadRequest(Arguments($$"""{"page":10,"mailbox_ids":["{{WORK.Id}}"]}"""), [WORK, PRIVATE], TimeZoneInfo.Utc));

        Assert.That(message, Does.Contain("must be at most 9"));
    }

    [Test]
    public void ASearchWhichBroughtNothingRequiresNothing()
    {
        var (confidence, outboundData) = MailToolResults.GetRequirements([]);

        Assert.Multiple(() =>
        {
            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.NONE));
            Assert.That(outboundData, Is.EqualTo(OutboundDataRequirement.NONE));
        });
    }

    [Test]
    public void TheStrictestMailboxDecides()
    {
        var (confidence, outboundData) = MailToolResults.GetRequirements([WORK, PRIVATE]);

        Assert.Multiple(() =>
        {
            Assert.That(confidence, Is.EqualTo(ConfidenceLevel.HIGH), "The private mail is in the chat now, so a provider has to be trusted as much as that mailbox asks.");
            Assert.That(outboundData, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, PRIVATE.Id)));
        });
    }

    [Test]
    public void OnATieTheFirstMailboxIsNamed()
    {
        var (_, outboundData) = MailToolResults.GetRequirements([WORK, WORK with { Id = "1b5e9c3a-7d2f-4e6b-a8c1-0f4d6e2b9a75" }]);

        Assert.That(outboundData.DataSourceId, Is.EqualTo(WORK.Id), "Otherwise the chat would name another mailbox with every search.");
    }

    [Test]
    public void TheConditionsShowHowTheDatesWereRead()
    {
        var timeZone = TimeZoneInfo.CreateCustomTimeZone("AI Studio test zone", TimeSpan.FromHours(2), "AI Studio test zone", "AI Studio test zone");
        var conditions = new MailConditions(new MailFilter
        {
            ReceivedSinceUtc = new DateTimeOffset(2026, 8, 31, 22, 0, 0, TimeSpan.Zero),
            Importance = MailImportance.HIGH,
        }, "INBOX");

        var description = MailToolResults.DescribeConditions(conditions, timeZone);

        Assert.Multiple(() =>
        {
            Assert.That(description["received_at_or_after"]!.GetValue<string>(), Is.EqualTo("2026-09-01T00:00+02:00"), "In the time zone of the user, as the model wrote it.");
            Assert.That(description[MailToolArguments.IMPORTANCE_ARGUMENT]!.GetValue<string>(), Is.EqualTo("high"), "Named as the argument takes it.");
            Assert.That(description[MailToolArguments.FOLDER_ARGUMENT]!.GetValue<string>(), Is.EqualTo("INBOX"));
            Assert.That(description.ContainsKey(MailToolArguments.FROM_ARGUMENT), Is.False, "A condition left out is not shown.");
        });
    }

    [Test]
    public void SemanticSearchNeverSeesAMailbox()
    {
        Assert.That(typeof(IDataSource).IsAssignableFrom(typeof(DataSourceMailbox)), Is.False, "Semantic Search, classic RAG, and the agents take their data sources from DataSources, which holds only IDataSource. A mailbox has its own rules and tools.");
    }

    // Stating its definition and reading its arguments needs none of the services the tool searches with:
    private SearchMailsTool Tool() => new(this.SettingsManager, new MailboxRetrievalService(this.SettingsManager, null!, null!, NullLogger<MailboxRetrievalService>.Instance), null!, NullLogger<SearchMailsTool>.Instance);

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}