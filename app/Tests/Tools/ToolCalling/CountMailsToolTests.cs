using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks what Count Mails accepts, and how it tells the numbers of the server from those of the index.
/// </summary>
/// <remarks>
/// "You have 12 unread mails" is only true for the period AI Studio indexes. The server knows how
/// many mails its folders hold altogether, so the result shows both, and a total the server did not
/// tell for every folder is no total at all.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class CountMailsToolTests : ToolRegistryTestBase
{
    private static readonly DataSourceMailbox WORK = new() { Num = 1, Id = "7d3a1e9c-5b2f-4c8e-a4d6-0e9b1f7c2a83", Name = "Work", ConfidenceLevel = ConfidenceLevel.MEDIUM };
    private static readonly DataSourceMailbox PRIVATE = new() { Num = 2, Id = "2f6c8a1d-9e3b-4d7a-b5c2-8a1e0f6d3b94", Name = "Private", ConfidenceLevel = ConfidenceLevel.HIGH };

    [TestCase("""{}""", MailCountGrouping.NONE)]
    [TestCase("""{"group_by":null}""", MailCountGrouping.NONE)]
    [TestCase("""{"group_by":"folder"}""", MailCountGrouping.FOLDER)]
    [TestCase("""{"group_by":"sender"}""", MailCountGrouping.SENDER)]
    public void TheNumberIsBrokenDownAsAskedFor(string json, MailCountGrouping expected)
    {
        Assert.That(CountMailsTool.ReadRequest(Arguments(json), [WORK, PRIVATE], TimeZoneInfo.Utc).Grouping, Is.EqualTo(expected));
    }

    [TestCase("""{"group_by":"mailbox"}""")]
    [TestCase("""{"group_by":"Sender"}""")]
    public void AnotherBreakdownIsRefused(string json)
    {
        var message = Refusal(() => CountMailsTool.ReadRequest(Arguments(json), [WORK], TimeZoneInfo.Utc));

        Assert.That(message, Does.Contain("'group_by' must be one of folder, sender").And.Contain("Leave it out for the totals only."), "Every mailbox is counted on its own anyway.");
    }

    [Test]
    public void TheConditionsAreThoseOfTheSearch()
    {
        var request = CountMailsTool.ReadRequest(Arguments($$"""{"mailbox_ids":["{{PRIVATE.Id}}"],"is_unread":true,"from":"alice"}"""), [WORK, PRIVATE], TimeZoneInfo.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(request.Mailboxes.Select(mailbox => mailbox.Id), Is.EqualTo(new[] { PRIVATE.Id }));
            Assert.That(request.Conditions.Filter, Is.EqualTo(new MailFilter { IsUnread = true, From = "alice" }), "So the mails counted are the ones a search with the same conditions lists.");
        });
    }

    [Test]
    public void TheServerCountsAddUpTheFoldersCounted()
    {
        IReadOnlyList<MailFolderRecord> folders = [Folder("INBOX", 120, 4), Folder("Archive", 900, 0), Folder("Projects", 30, 2)];

        var all = CountMailsTool.GetServerCounts(folders, folderPaths: null);
        var someOfThem = CountMailsTool.GetServerCounts(folders, ["INBOX", "Projects"]);

        Assert.Multiple(() =>
        {
            Assert.That(all, Is.EqualTo((1_050L, 6L)));
            Assert.That(someOfThem, Is.EqualTo((150L, 6L)), "With a folder condition, only those folders count.");
        });
    }

    [Test]
    public void AFolderTheServerDidNotTellAboutLeavesNoTotal()
    {
        IReadOnlyList<MailFolderRecord> folders = [Folder("INBOX", 120, 4), Folder("Archive", null, null)];

        Assert.Multiple(() =>
        {
            Assert.That(CountMailsTool.GetServerCounts(folders, folderPaths: null), Is.Null, "A sum over some of the folders would be too low without saying so.");
            Assert.That(CountMailsTool.GetServerCounts(folders, ["INBOX"]), Is.EqualTo((120L, 4L)), "The folders which are known still count on their own.");
            Assert.That(CountMailsTool.GetServerCounts([], folderPaths: null), Is.Null, "Without a folder, there is nothing to add up.");
        });
    }

    [Test]
    public void AFolderGroupTellsWhetherItHoldsTheSentMailsOrTheDrafts()
    {
        var coverage = new MailboxCoverage(null, null, null, null, [Folder("INBOX", 120, 4), Folder("Sent Items", 40, 0, MailFolderSpecialUse.SENT), Folder("Drafts", 3, 0, MailFolderSpecialUse.DRAFTS)]);

        var sent = CountMailsTool.DescribeGroup(new MailCountGroup("Sent Items", string.Empty, 40), "Sent Items", MailCountGrouping.FOLDER, coverage);
        var drafts = CountMailsTool.DescribeGroup(new MailCountGroup("Drafts", string.Empty, 3), "Drafts", MailCountGrouping.FOLDER, coverage);
        var inbox = CountMailsTool.DescribeGroup(new MailCountGroup("INBOX", string.Empty, 12), "INBOX", MailCountGrouping.FOLDER, coverage);

        Assert.Multiple(() =>
        {
            Assert.That(sent[MailToolArguments.SPECIAL_FOLDER_ARGUMENT]?.GetValue<string>(), Is.EqualTo("sent"));
            Assert.That(sent["server_message_count"]?.GetValue<long>(), Is.EqualTo(40), "The numbers of the server stay.");
            Assert.That(drafts[MailToolArguments.SPECIAL_FOLDER_ARGUMENT]?.GetValue<string>(), Is.EqualTo("drafts"));
            Assert.That(inbox.ContainsKey(MailToolArguments.SPECIAL_FOLDER_ARGUMENT), Is.False);
        });
    }

    [Test]
    public void TheSpecialFolderIsCountedLikeTheSearchFindsIt()
    {
        var request = CountMailsTool.ReadRequest(Arguments("""{"special_folder":"sent","to":"alice"}"""), [WORK], TimeZoneInfo.Utc);

        Assert.That(request.Conditions.SpecialFolder, Is.EqualTo(MailFolderSpecialUse.SENT));
    }

    [Test]
    public void TheFunctionOffersExactlyTheMailboxesGiven()
    {
        var function = CountMailsTool.DescribeMailboxes(this.Tool().GetDefinition().Function, [WORK, PRIVATE]);
        var properties = JsonNode.Parse(function.Parameters.GetRawText())!["properties"]!;

        Assert.Multiple(() =>
        {
            Assert.That(function.DescriptionForLLM, Does.Contain($"- id={WORK.Id}, name='Work'").And.Contain($"- id={PRIVATE.Id}, name='Private'"));
            Assert.That(properties[MailToolArguments.MAILBOX_IDS_ARGUMENT]!["items"]!["enum"]!.AsArray().Select(id => id!.GetValue<string>()), Is.EqualTo(new[] { WORK.Id, PRIVATE.Id }));
            Assert.That(properties["group_by"]!["enum"]!.AsArray().Select(value => value!.GetValue<string>()), Is.EqualTo(new[] { "folder", "sender" }));
            Assert.That(properties["query"], Is.Null, "A count needs no query.");
        });
    }

    [Test]
    public async Task TheRegistryTakesTheDefinition()
    {
        var registry = this.CreateRegistry(new TestTool(this.Tool().GetDefinition()));

        var runnableTools = await registry.GetRunnableToolsAsync(this.ContextFor(ToolCapableProvider()), [ToolSelectionRules.COUNT_MAILS_TOOL_ID], mayRunTools: true);

        Assert.That(runnableTools.Select(tool => tool.Definition.Id), Is.EqualTo(new[] { ToolSelectionRules.COUNT_MAILS_TOOL_ID }), "The registry drops a definition it cannot accept, with no more than a warning in the log.");
    }

    [Test]
    public void TheToolKeepsToTheRulesOfAMailbox()
    {
        var tool = this.Tool();

        Assert.Multiple(() =>
        {
            Assert.That(tool.IsAvailable, Is.False, "Without the previews, the tool does not exist.");
            Assert.That(new MailboxToolCollection().GetDefinition().ToolIds, Does.Contain(ToolSelectionRules.COUNT_MAILS_TOOL_ID), "The mailbox collection states the confidence the tool needs.");
            Assert.That(tool.OutboundData, Is.EqualTo(ToolOutboundData.NONE), "Counting sends no query anywhere.");
            Assert.That(tool.ReturnsUntrustedExternalContent, Is.True, "The names of senders and folders were written by others.");
        });
    }

    // Stating its definition and reading its arguments needs none of the services the tool counts with:
    private CountMailsTool Tool() => new(this.SettingsManager, new MailboxRetrievalService(this.SettingsManager, null!, null!, NullLogger<MailboxRetrievalService>.Instance), null!, null!, NullLogger<CountMailsTool>.Instance);

    private static MailFolderRecord Folder(string path, long? messageCount, long? unseenCount, MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE) => new(path, specialUse, 1, 100, null, messageCount, unseenCount, null);

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}