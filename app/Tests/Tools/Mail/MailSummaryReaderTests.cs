using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Mail;

using MailKit;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks what AI Studio keeps of the summary a server reports about a mail.
/// </summary>
/// <remarks>
/// The summaries are built by hand, with the header blocks of the fixtures: the sync fetches the
/// whole header block with every mail and reads everything from there.
/// </remarks>
[TestFixture]
public sealed class MailSummaryReaderTests
{
    /// <summary>
    /// Where a part sits in the mail, e.g. "1.2". Nothing here looks at it.
    /// </summary>
    private const string PART_SPECIFIER = "1";

    [Test]
    public void TheIdentityComesFromTheHeaderBlock()
    {
        var summary = Summary("header-injection.eml");
        summary.EmailId = "M6d99ac3275bb4e";
        summary.Size = 2048;

        var identity = MailSummaryReader.ReadIdentity(summary);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(identity.EmailId, Is.EqualTo("M6d99ac3275bb4e"));
            Assert.That(identity.GmailMessageId, Is.Null);
            Assert.That(identity.MessageId, Is.EqualTo("<header-injection@example.org>"));
            Assert.That(identity.Date, Is.EqualTo(new DateTimeOffset(2026, 9, 30, 13, 0, 0, TimeSpan.FromHours(2))));
            Assert.That(identity.FromAddresses, Is.EqualTo(new[] { "mueller@example.org" }));
            Assert.That(identity.Subject, Does.StartWith("Hello"));
            Assert.That(identity.Size, Is.EqualTo(2048));
        }
    }

    /// <remarks>
    /// The keys come from what MimeKit reads out of the header block. A version of MimeKit which reads
    /// a header in another way would give every mail it concerns a new key and embed it anew, so these
    /// keys stay as MimeKit 4.18.2 made them. Change a pinned value only on purpose.
    /// </remarks>
    [TestCase("header-injection.eml", "mail:4ad6f665ee9a7b698d32276855cc2c8da31e5c7d25057c45d15484712ee045ad")]
    [TestCase("priority-with-attachment.eml", "mail:797ac20c552b0aed439f87095a2345f38a5268a7656024b5394349652ac08e18")]
    [TestCase("idn-sender.eml", "mail:e7284cb3afe9041918d344de00e01f55aebc7b29ebcd25967a8810eca13068d8")]
    public void TheKeysOfIndexedMailsStayTheSame(string fixtureName, string expectedKey)
    {
        var summary = Summary(fixtureName);
        summary.Size = 4096;

        Assert.That(MailContentKey.Create(MailSummaryReader.ReadIdentity(summary)), Is.EqualTo(expectedKey));
    }

    [Test]
    public void AnInternationalDomainIsReadInUnicode()
    {
        var summary = Summary("idn-sender.eml");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailSummaryReader.ReadIdentity(summary).FromAddresses, Is.EqualTo(new[] { "juergen@müller.example" }));
            Assert.That(MailSummaryReader.ReadAddresses(summary.Headers!), Is.EqualTo(new[]
            {
                new MailAddressRecord(MailAddressRole.FROM, "juergen@müller.example", "Müller, Jürgen"),
                new MailAddressRecord(MailAddressRole.TO, "team@example.org", "Team"),
                new MailAddressRecord(MailAddressRole.CC, "info@bücher.example", "Library"),
            }), "The index keeps an address in the form the mails indexed before keep it.");
        }
    }

    [Test]
    public void EveryAddressKeepsItsHeader()
    {
        var addresses = MailSummaryReader.ReadAddresses(MailFixtures.Load("header-injection.eml").Headers);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(addresses.Count(address => address.Role is MailAddressRole.TO), Is.EqualTo(12), "An address of the second To line is lost.");
            Assert.That(addresses[0], Is.EqualTo(new MailAddressRecord(MailAddressRole.FROM, "mueller@example.org", "Müller, Jürgen")));
            Assert.That(addresses[^1], Is.EqualTo(new MailAddressRecord(MailAddressRole.CC, "mueller@example.org", "mueller@example.org")));
            Assert.That(addresses.Any(address => address.Role is MailAddressRole.SENDER or MailAddressRole.REPLY_TO), Is.False, "A header the mail does not have was made up.");
        }
    }

    [Test]
    public void SenderAndReplyToCountOnlyWhenTheMailSetsThem()
    {
        var headers = new HeaderList();
        headers.Add("From", "Alice <alice@example.org>");
        headers.Add("Sender", "Assistant <assistant@example.org>");
        headers.Add("Reply-To", "Team <team@example.org>");

        Assert.That(MailSummaryReader.ReadAddresses(headers), Is.EqualTo(new[]
        {
            new MailAddressRecord(MailAddressRole.FROM, "alice@example.org", "Alice"),
            new MailAddressRecord(MailAddressRole.SENDER, "assistant@example.org", "Assistant"),
            new MailAddressRecord(MailAddressRole.REPLY_TO, "team@example.org", "Team"),
        }));
    }

    [Test]
    public void TheTextSourceTakesAttachmentsAndEncryptionFromTheStructure()
    {
        var attachment = new BodyPartBasic(new ContentType("application", "pdf"), PART_SPECIFIER)
        {
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "board-report.pdf" },
        };

        var readable = Summary("priority-with-attachment.eml");
        readable.Body = Multipart("mixed", new BodyPartText(new ContentType("text", "plain"), PART_SPECIFIER), attachment);

        var encrypted = Summary("smime-enveloped.eml");
        encrypted.Body = new BodyPartBasic(new ContentType("application", "pkcs7-mime") { Parameters = { { "smime-type", "enveloped-data" } } }, PART_SPECIFIER);

        var readableSource = MailSummaryReader.ReadTextSource(readable, new MailTextParts(null, "Please review the attached report."));
        var encryptedSource = MailSummaryReader.ReadTextSource(encrypted, null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(readableSource.AttachmentNames, Is.EqualTo(new[] { "board-report.pdf" }));
            Assert.That(readableSource.StructureEncryption, Is.EqualTo(MailEncryptionKind.NONE));
            Assert.That(readableSource.TextBody, Is.EqualTo("Please review the attached report."));
            Assert.That(readableSource.HtmlBody, Is.Null);
            Assert.That(encryptedSource.StructureEncryption, Is.EqualTo(MailEncryptionKind.SMIME));
            Assert.That(MailTextBuilder.Build(encryptedSource).Body, Is.Empty);
        }
    }

    [Test]
    public void TheSignatureOfASignedMailIsNoAttachment()
    {
        var signature = new BodyPartBasic(new ContentType("application", "pkcs7-signature"), PART_SPECIFIER)
        {
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = "smime.p7s" },
        };

        var signed = Summary("smime-clear-signed.eml");
        signed.Body = Multipart("signed", new BodyPartText(new ContentType("text", "plain"), PART_SPECIFIER), signature);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailSummaryReader.ReadAttachments(signed), Is.Empty);
            Assert.That(MailSummaryReader.ReadTextSource(signed, new MailTextParts(null, "Signed, but readable for everybody.")).AttachmentNames, Is.Empty);
        }
    }

    [Test]
    public void TheFlagsAreRead()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailSummaryReader.ReadFlags(MessageFlags.Seen | MessageFlags.Flagged | MessageFlags.Recent), Is.EqualTo(new MailFlags(IsSeen: true, IsFlagged: true, IsAnswered: false)));
            Assert.That(MailSummaryReader.ReadFlags(MessageFlags.Answered), Is.EqualTo(new MailFlags(IsSeen: false, IsFlagged: false, IsAnswered: true)));
            Assert.That(MailSummaryReader.ReadFlags(null), Is.EqualTo(new MailFlags(IsSeen: false, IsFlagged: false, IsAnswered: false)));
        }
    }

    [Test]
    public void TheHeaderBlockStaysAsTheServerSentIt() =>
        Assert.That(MailSummaryReader.ReadHeaderBlock(MailFixtures.Load("header-injection.eml").Headers), Does.Contain("Subject: =?utf-8?b?SGVsbG8NCkZyb206IGNlb0BleGFtcGxlLm9yZw==?="), "The trust check needs the encoded words as they were sent.");

    [Test]
    public void TheMailHashFollowsTheMail()
    {
        var summary = Summary("priority-with-attachment.eml");
        summary.Size = 2048;
        var hash = MailSummaryReader.ComputeMailHash(summary);

        var sameMail = Summary("priority-with-attachment.eml");
        sameMail.Size = 2048;

        var otherSize = Summary("priority-with-attachment.eml");
        otherSize.Size = 2049;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(MailSummaryReader.ComputeMailHash(sameMail), Is.EqualTo(hash));
            Assert.That(MailSummaryReader.ComputeMailHash(otherSize), Is.Not.EqualTo(hash));
            Assert.That(MailSummaryReader.ComputeMailHash(Summary("smime-enveloped.eml")), Is.Not.EqualTo(hash));
        }
    }

    [Test]
    public void MessageIdsAreReadWithoutTheirBrackets()
    {
        var headers = new HeaderList();
        headers.Add("In-Reply-To", "<second@example.org>");
        headers.Add("References", "<first@example.org>\r\n <second@example.org>");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailHeaders.ReadMessageIds(headers, HeaderId.References), Is.EqualTo(new[] { "first@example.org", "second@example.org" }));
            Assert.That(MailHeaders.ReadMessageIds(headers, HeaderId.InReplyTo), Is.EqualTo(new[] { "second@example.org" }));
            Assert.That(MailHeaders.ReadMessageIds(headers, HeaderId.MessageId), Is.Empty);
        }
    }

    [Test]
    public void AMailFetchedWithoutItsHeaderBlockIsRefused()
    {
        //
        // The IMAP client hands over an empty header block when it cannot parse the one the server sent:
        //
        var withEmptyHeaderBlock = new MessageSummary(0) { Headers = new HeaderList() };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MailSummaryReader.ReadHeaders(new MessageSummary(0)), Is.Null);
            Assert.That(MailSummaryReader.ReadHeaders(withEmptyHeaderBlock), Is.Null, "An empty header block counts as one which could be read.");
            Assert.That(() => MailSummaryReader.ReadIdentity(new MessageSummary(0)), Throws.InvalidOperationException);
            Assert.That(() => MailSummaryReader.ReadIdentity(withEmptyHeaderBlock), Throws.InvalidOperationException);
        }
    }

    private static MessageSummary Summary(string fixtureName) => new(0) { Headers = MailFixtures.Load(fixtureName).Headers };

    private static BodyPartMultipart Multipart(string mediaSubtype, params BodyPart[] parts)
    {
        var multipart = new BodyPartMultipart(new ContentType("multipart", mediaSubtype), PART_SPECIFIER);
        foreach (var part in parts)
            multipart.BodyParts.Add(part);

        return multipart;
    }
}