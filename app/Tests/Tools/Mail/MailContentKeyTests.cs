using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks the key a mail gets in the index.
/// </summary>
/// <remarks>
/// The keys are pinned: every stored chunk of a mail hangs on its key, so a key which changes
/// between two versions embeds every mailbox anew. Change a pinned value only on purpose.
/// </remarks>
[TestFixture]
public sealed class MailContentKeyTests
{
    private static readonly MailIdentity HEADERS_ONLY = new(
        EmailId: null,
        GmailMessageId: null,
        MessageId: "<abc@example.org>",
        Date: new DateTimeOffset(2026, 9, 30, 14, 12, 0, TimeSpan.FromHours(2)),
        FromAddresses: ["Alice@Example.org"],
        Subject: "Quarterly report",
        Size: 2048);

    [Test]
    public void TheKeysArePinned()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { EmailId = "M6d99ac3275bb4e" }), Is.EqualTo("mail:32b3f32a39c11c2c2d4fe3b06d86a8446075fb6d910ba960e3637665515bc93a"));
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { GmailMessageId = 1278455344230334865 }), Is.EqualTo("mail:b117963778cdad685e4382da37292cb2e6f67e7e8a33f0e9107e1cd386b32d50"));
            Assert.That(MailContentKey.Create(HEADERS_ONLY), Is.EqualTo("mail:4e0ba3a77899611b9731d137320cc98df8e66f5ea88d158117958d6e1f2cbae1"));
        });
    }

    [Test]
    public void TheIdsOfTheServerComeBeforeTheHeaders()
    {
        var withEmailId = HEADERS_ONLY with { EmailId = "M6d99ac3275bb4e", GmailMessageId = 1278455344230334865 };
        var withGmailId = HEADERS_ONLY with { GmailMessageId = 1278455344230334865 };

        Assert.Multiple(() =>
        {
            Assert.That(MailContentKey.Create(withEmailId with { GmailMessageId = null, Subject = "Another subject", Size = 1 }), Is.EqualTo(MailContentKey.Create(withEmailId)), "The EMAILID does not decide alone.");
            Assert.That(MailContentKey.Create(withGmailId with { Subject = "Another subject", Size = 1 }), Is.EqualTo(MailContentKey.Create(withGmailId)), "The X-GM-MSGID does not decide alone.");
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { EmailId = "   " }), Is.EqualTo(MailContentKey.Create(HEADERS_ONLY)), "An empty EMAILID is used.");
        });
    }

    [Test]
    public void AnEmailIdNeverMeetsAGmailId() =>
        Assert.That(MailContentKey.Create(HEADERS_ONLY with { EmailId = "123" }), Is.Not.EqualTo(MailContentKey.Create(HEADERS_ONLY with { GmailMessageId = 123 })));

    [Test]
    public void TheHeaderKeyIgnoresWhatReadersSpellDifferently()
    {
        var spelledDifferently = HEADERS_ONLY with
        {
            MessageId = "abc@example.org",
            Date = new DateTimeOffset(2026, 9, 30, 12, 12, 0, TimeSpan.Zero),
            FromAddresses = [" alice@example.org "],
            Subject = "Quarterly \r\n report",
        };

        Assert.That(MailContentKey.Create(spelledDifferently), Is.EqualTo(MailContentKey.Create(HEADERS_ONLY)));
    }

    [Test]
    public void TheHeaderKeyTellsMailsApart()
    {
        var key = MailContentKey.Create(HEADERS_ONLY);
        Assert.Multiple(() =>
        {
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { Size = 2049 }), Is.Not.EqualTo(key));
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { Subject = "Quarterly report, corrected" }), Is.Not.EqualTo(key));
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { Date = null }), Is.Not.EqualTo(key));
            Assert.That(MailContentKey.Create(HEADERS_ONLY with { FromAddresses = ["bob@example.org"] }), Is.Not.EqualTo(key));
        });
    }
}