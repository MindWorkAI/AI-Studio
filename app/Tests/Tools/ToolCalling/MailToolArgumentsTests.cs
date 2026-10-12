using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.Mailboxes;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how the mail tools read the conditions a model sets for the mails.
/// </summary>
/// <remarks>
/// "Unread mails from Alice since Monday" has to become exactly these conditions, or the answer
/// tells the user something wrong with confidence. So every condition is read strictly, and one
/// that cannot be meant as written is refused with what would have been right.
/// </remarks>
[TestFixture]
public sealed class MailToolArgumentsTests
{
    private static readonly DataSourceMailbox WORK = new() { Id = "2b7e4c1a-9d3f-4e8b-a6c5-1f0d8e3b7a92", Name = "Work" };
    private static readonly DataSourceMailbox PRIVATE = new() { Id = "7c1d9e4b-3a8f-4b2e-9d6c-5e0a2f8b1c73", Name = "Private" };
    private static readonly IReadOnlyList<DataSourceMailbox> OFFERED = [WORK, PRIVATE];

    [Test]
    public void EveryConditionBecomesPartOfTheFilter()
    {
        var conditions = MailToolArguments.ReadConditions(Arguments("""
            {
              "from": " alice@example.org ",
              "to": "Bob",
              "after": "2026-09-01",
              "before": "2026-09-30T18:00",
              "is_unread": true,
              "is_flagged": false,
              "is_encrypted": false,
              "importance": "high",
              "has_attachments": true,
              "folder": "Archive/2026"
            }
            """), UserTimeZone());

        Assert.Multiple(() =>
        {
            Assert.That(conditions.Filter, Is.EqualTo(new MailFilter
            {
                From = "alice@example.org",
                To = "Bob",
                ReceivedSinceUtc = new DateTimeOffset(2026, 8, 31, 22, 0, 0, TimeSpan.Zero),
                ReceivedBeforeUtc = new DateTimeOffset(2026, 9, 30, 16, 0, 0, TimeSpan.Zero),
                IsUnread = true,
                IsFlagged = false,
                IsEncrypted = false,
                Importance = MailImportance.HIGH,
                HasAttachments = true,
            }));
            Assert.That(conditions.Folder, Is.EqualTo("Archive/2026"));
        });
    }

    [TestCase("""{}""")]
    [TestCase("""{"from":null,"to":null,"after":null,"before":null,"is_unread":null,"is_flagged":null,"is_encrypted":null,"importance":null,"has_attachments":null,"folder":null,"special_folder":null}""")]
    public void ConditionsLeftOutHoldForEveryMail(string json)
    {
        var conditions = MailToolArguments.ReadConditions(Arguments(json), UserTimeZone());

        Assert.Multiple(() =>
        {
            Assert.That(conditions.Filter.HasConditions, Is.False, "A strict schema makes the model pass null for every condition it does not want.");
            Assert.That(conditions.Folder, Is.Null);
            Assert.That(conditions.SpecialFolder, Is.Null);
            Assert.That(conditions.NamesFolder, Is.False);
        });
    }

    [TestCase("sent", MailFolderSpecialUse.SENT)]
    [TestCase("drafts", MailFolderSpecialUse.DRAFTS)]
    public void ASpecialFolderIsReadAsWhatTheFolderIsFor(string value, MailFolderSpecialUse expected)
    {
        var conditions = MailToolArguments.ReadConditions(Arguments($$"""{"special_folder":"{{value}}"}"""), UserTimeZone());

        Assert.Multiple(() =>
        {
            Assert.That(conditions.SpecialFolder, Is.EqualTo(expected));
            Assert.That(conditions.Folder, Is.Null);
            Assert.That(conditions.NamesFolder, Is.True);
            Assert.That(MailToolArguments.ToArgumentValue(expected), Is.EqualTo(value), "Results mark the mails with the value the argument takes.");
        });
    }

    [Test]
    public void AFolderAndASpecialFolderCannotBeCombined()
    {
        var message = Refusal(() => MailToolArguments.ReadConditions(Arguments("""{"folder":"Sent Items","special_folder":"sent"}"""), UserTimeZone()));

        Assert.That(message, Does.Contain("'folder' and 'special_folder' cannot be combined").And.Contain("Leave out 'folder'").And.Contain("or leave out 'special_folder'"), "Two folders would match no mail at all.");
    }

    [TestCase("2026-09-30", "2026-09-01")]
    [TestCase("2026-09-01", "2026-09-01")]
    public void APeriodWhichEndsBeforeItStartsIsRefused(string after, string before)
    {
        var message = Refusal(() => MailToolArguments.ReadConditions(Arguments($$"""{"after":"{{after}}","before":"{{before}}"}"""), UserTimeZone()));

        Assert.That(message, Does.Contain("'after' must lie before argument 'before'").And.Contain("Swap the two, or leave one of them out."));
    }

    [TestCase("""{"importance":"urgent"}""", "'importance' must be one of low, normal, high")]
    [TestCase("""{"importance":"HIGH"}""", "'importance' must be one of low, normal, high")]
    [TestCase("""{"is_unread":"true"}""", "'is_unread' must be true or false")]
    [TestCase("""{"after":"last week"}""", "'after' must be a date")]
    [TestCase("""{"from":""}""", "'from' must not be empty")]
    [TestCase("""{"folder":"INBOX\nArchive"}""", "'folder' must not contain control characters")]
    [TestCase("""{"special_folder":"inbox"}""", "'special_folder' must be one of sent, drafts")]
    [TestCase("""{"special_folder":"Sent"}""", "'special_folder' must be one of sent, drafts")]
    public void AConditionWhichCannotBeMeantAsWrittenIsRefused(string json, string expectedMessage)
    {
        var message = Refusal(() => MailToolArguments.ReadConditions(Arguments(json), UserTimeZone()));

        Assert.That(message, Does.Contain(expectedMessage).And.Contain("Leave it out"));
    }

    [Test]
    public void TheModelPicksSomeOfTheOfferedMailboxes()
    {
        var all = MailToolArguments.ReadMailboxes(Arguments("""{}"""), OFFERED, "search");
        var some = MailToolArguments.ReadMailboxes(Arguments($$"""{"mailbox_ids":["{{PRIVATE.Id}}"]}"""), OFFERED, "search");

        Assert.Multiple(() =>
        {
            Assert.That(all.Select(mailbox => mailbox.Id), Is.EqualTo(new[] { WORK.Id, PRIVATE.Id }));
            Assert.That(some.Select(mailbox => mailbox.Id), Is.EqualTo(new[] { PRIVATE.Id }));
        });
    }

    [Test]
    public void AMailboxNotOfferedIsRefused()
    {
        var message = Refusal(() => MailToolArguments.ReadMailboxes(Arguments("""{"mailbox_ids":["3f9a1c7e-5b2d-4e8a-b1c6-9d0e7f2a4b58"]}"""), OFFERED, "search"));

        Assert.That(message, Does.Contain(WORK.Id).And.Contain(PRIVATE.Id).And.Contain("Leave it out to search all listed mailboxes."), "The refusal names the mailboxes the model may search.");
    }

    [Test]
    public void AFolderIsFoundByItsFullPathRegardlessOfCase()
    {
        var folders = new[] { Folder("INBOX"), Folder("INBOX/Projects"), Folder("Archive"), Folder("archive") };

        var inbox = new MailConditions(new MailFilter(), "Inbox", null).ForMailbox(folders);
        var archive = new MailConditions(new MailFilter(), "ARCHIVE", null).ForMailbox(folders);

        Assert.Multiple(() =>
        {
            Assert.That(inbox.FolderPaths, Is.EqualTo(new[] { "INBOX" }), "Its subfolders are folders of their own.");
            Assert.That(archive.FolderPaths, Is.EqualTo(new[] { "Archive", "archive" }), "The model cannot tell two folders apart which differ only in case.");
        });
    }

    [Test]
    public void AFolderTheMailboxDoesNotHaveMatchesNoMail()
    {
        var filter = new MailConditions(new MailFilter { IsUnread = true }, "Projects", null).ForMailbox([Folder("INBOX")]);

        Assert.Multiple(() =>
        {
            Assert.That(filter.FolderPaths, Is.Empty, "An empty list matches no mail; a missing one would match every mail.");
            Assert.That(filter.IsUnread, Is.True, "The other conditions stay.");
        });
    }

    [Test]
    public void ASpecialFolderIsFoundByWhatTheServerMarksItAsWhateverItsName()
    {
        var folders = new[] { Folder("INBOX"), Folder("Gesendete Elemente", MailFolderSpecialUse.SENT), Folder("Sent"), Folder("Entwürfe", MailFolderSpecialUse.DRAFTS) };

        var sent = new MailConditions(new MailFilter(), null, MailFolderSpecialUse.SENT).ForMailbox(folders);
        var drafts = new MailConditions(new MailFilter(), null, MailFolderSpecialUse.DRAFTS).ForMailbox(folders);

        Assert.Multiple(() =>
        {
            Assert.That(sent.FolderPaths, Is.EqualTo(new[] { "Gesendete Elemente" }), "A folder merely named like the sent mails is not theirs.");
            Assert.That(drafts.FolderPaths, Is.EqualTo(new[] { "Entwürfe" }));
        });
    }

    [Test]
    public void ASpecialFolderTheMailboxDoesNotHaveMatchesNoMail()
    {
        var filter = new MailConditions(new MailFilter(), null, MailFolderSpecialUse.DRAFTS).ForMailbox([Folder("INBOX"), Folder("Drafts")]);

        Assert.That(filter.FolderPaths, Is.Empty, "A mailbox which left out its drafts, or whose server does not mark them, must not get the whole mailbox back.");
    }

    [Test]
    public void WithoutAFolderEveryFolderCounts()
    {
        Assert.That(new MailConditions(new MailFilter(), null, null).ForMailbox([Folder("INBOX")]).FolderPaths, Is.Null);
    }

    [Test]
    public void TheSchemaOffersEveryConditionAndOnlyTheOfferedMailboxes()
    {
        var schema = JsonNode.Parse(ToolParameterSchemaBuilder.Create().AddMailConditions("search", WORK.Id, PRIVATE.Id).Build().GetRawText())!;
        var properties = schema["properties"]!.AsObject();

        Assert.Multiple(() =>
        {
            Assert.That(properties.Select(property => property.Key), Is.EquivalentTo(new[]
            {
                MailToolArguments.MAILBOX_IDS_ARGUMENT, MailToolArguments.FROM_ARGUMENT, MailToolArguments.TO_ARGUMENT, MailToolArguments.AFTER_ARGUMENT, MailToolArguments.BEFORE_ARGUMENT,
                MailToolArguments.IS_UNREAD_ARGUMENT, MailToolArguments.IS_FLAGGED_ARGUMENT, MailToolArguments.IS_ENCRYPTED_ARGUMENT, MailToolArguments.IMPORTANCE_ARGUMENT,
                MailToolArguments.HAS_ATTACHMENTS_ARGUMENT, MailToolArguments.FOLDER_ARGUMENT, MailToolArguments.SPECIAL_FOLDER_ARGUMENT,
            }));
            Assert.That(properties[MailToolArguments.SPECIAL_FOLDER_ARGUMENT]!["enum"]!.AsArray().Select(value => value?.GetValue<string>()), Is.EqualTo(new[] { "sent", "drafts" }));
            Assert.That(properties[MailToolArguments.MAILBOX_IDS_ARGUMENT]!["items"]!["enum"]!.AsArray().Select(id => id!.GetValue<string>()), Is.EqualTo(new[] { WORK.Id, PRIVATE.Id }));
            Assert.That(schema["required"]!.AsArray(), Is.Empty, "Every condition may be left out.");
        });
    }

    private static MailFolderRecord Folder(string path, MailFolderSpecialUse specialUse = MailFolderSpecialUse.NONE) => new(path, specialUse, 1, null, null, null, null, null);

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;

    /// <summary>
    /// A time zone two hours ahead of UTC, all year round, so that no test depends on the computer or on daylight saving time.
    /// </summary>
    private static TimeZoneInfo UserTimeZone() => TimeZoneInfo.CreateCustomTimeZone("AI Studio test zone", TimeSpan.FromHours(2), "AI Studio test zone", "AI Studio test zone");
}