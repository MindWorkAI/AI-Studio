using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how the mailboxes a chat read from restrict where it may still send data.
/// </summary>
/// <remarks>
/// Once mail content is in a chat, every tool which runs afterwards could carry it out. The chat
/// therefore keeps the strictest restriction of every mailbox it read from, and it keeps it beyond
/// a restart, because the content stays in the chat as well.
/// </remarks>
[TestFixture]
public sealed class ChatThreadOutboundDataRestrictionTests
{
    private const string PRIVATE_MAILBOX_ID = "5c9e2a7b-3f1d-4e8a-b6c4-2d7f9a1e3b5c";
    private const string WORK_MAILBOX_ID = "8d1f4b6a-2e9c-4a7d-9b3e-6c5a8f2d1e4b";

    [Test]
    public void AChatWhichReadNoMailboxDemandsNothing() =>
        Assert.That(new ChatThread().RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE));

    [TestCase(OutboundDataRestriction.UNRESTRICTED, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.UNRESTRICTED, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataRestriction.ONLY_CONFIGURED_SERVICES)]
    public void AStricterMailboxTightensTheChat(OutboundDataRestriction held, OutboundDataRestriction arriving)
    {
        var thread = new ChatThread();
        thread.RequireOutboundDataRestriction(new(held, PRIVATE_MAILBOX_ID));
        thread.RequireOutboundDataRestriction(new(arriving, WORK_MAILBOX_ID));

        Assert.That(thread.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(arriving, WORK_MAILBOX_ID)), "The stricter mailbox is the one which keeps a tool from running, so it is the one to name.");
    }

    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT)]
    [TestCase(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataRestriction.UNRESTRICTED)]
    [TestCase(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataRestriction.UNRESTRICTED)]
    public void ARestrictionStays(OutboundDataRestriction held, OutboundDataRestriction arriving)
    {
        var thread = new ChatThread();
        thread.RequireOutboundDataRestriction(new(held, PRIVATE_MAILBOX_ID));
        thread.RequireOutboundDataRestriction(new(arriving, WORK_MAILBOX_ID));

        Assert.That(thread.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(held, PRIVATE_MAILBOX_ID)), "The mail content was read by this chat. A more permissive mailbox read later cannot undo that.");
    }

    [Test]
    public void OnATieTheMailboxReadFirstStaysNamed()
    {
        var thread = new ChatThread();
        thread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, PRIVATE_MAILBOX_ID));
        thread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WORK_MAILBOX_ID));

        Assert.That(thread.RequiredOutboundDataRestriction.DataSourceId, Is.EqualTo(PRIVATE_MAILBOX_ID), "Otherwise the chat would name another mailbox with every search.");
    }

    [Test]
    public void ResultsWhichDemandNothingChangeNothing()
    {
        var thread = new ChatThread();
        thread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, PRIVATE_MAILBOX_ID));
        thread.RequireOutboundDataRestriction(OutboundDataRequirement.NONE);

        Assert.That(thread.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, PRIVATE_MAILBOX_ID)), "A search of the local files, say, says nothing about mailboxes and must leave the chat as it was.");
    }

    [Test]
    public void TheRestrictionSurvivesSavingTheChat()
    {
        var thread = new ChatThread { ChatId = Guid.NewGuid() };
        thread.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WORK_MAILBOX_ID));

        var loaded = JsonSerializer.Deserialize<ChatThread>(JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS), WorkspaceBehaviour.JSON_OPTIONS);

        Assert.That(loaded?.RequiredOutboundDataRestriction, Is.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, WORK_MAILBOX_ID)), "After a restart, the mail content is still in the chat, so the restriction has to be as well.");
    }

    [Test]
    public void AChatSavedBeforeMailboxesExistedDemandsNothing()
    {
        const string CHAT_WITHOUT_RESTRICTION = """
                                                {
                                                  "chat_id": "2b7c9e1f-4a6d-4c8b-9e3f-1d5a7c9b2e4f",
                                                  "name": "An older chat",
                                                  "blocks": []
                                                }
                                                """;

        var loaded = JsonSerializer.Deserialize<ChatThread>(CHAT_WITHOUT_RESTRICTION, WorkspaceBehaviour.JSON_OPTIONS);

        Assert.That(loaded?.RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE));
    }
}