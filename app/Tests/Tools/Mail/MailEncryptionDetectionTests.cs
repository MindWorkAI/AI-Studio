using AIStudio.Tools.Mail;

using MailKit;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks that the structure the server describes tells the same as the parsed mail.
/// </summary>
/// <remarks>
/// The sync decides from the BODYSTRUCTURE whether to fetch a mail at all, while the fixtures of
/// MailTextBuilderTests check the parsed mails. Both have to agree, or a mail would be fetched,
/// indexed and only then found to be encrypted, or the other way round.
/// </remarks>
[TestFixture]
public sealed class MailEncryptionDetectionTests
{
    /// <summary>
    /// Where a part sits in the mail, e.g. "1.2". The detection never looks at it.
    /// </summary>
    private const string PART_SPECIFIER = "1";

    private static readonly BodyPartBasic PLAIN_TEXT = Part("text", "plain");

    [Test]
    public void TheStructureFromTheServerTellsTheEncryption()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailEncryptionDetection.Detect(Part("application", "pkcs7-mime", smimeType: "enveloped-data")), Is.EqualTo(MailEncryptionKind.SMIME));
            Assert.That(MailEncryptionDetection.Detect(Part("application", "x-pkcs7-mime")), Is.EqualTo(MailEncryptionKind.SMIME), "An S/MIME part without smime-type is readable.");
            Assert.That(MailEncryptionDetection.Detect(Part("application", "pkcs7-mime", smimeType: "signed-data")), Is.EqualTo(MailEncryptionKind.SMIME_OPAQUE_SIGNED));
            Assert.That(MailEncryptionDetection.Detect(Part("application", "octet-stream", name: "smime.p7m")), Is.EqualTo(MailEncryptionKind.SMIME), "An envelope sent as plain bytes is readable.");
            Assert.That(MailEncryptionDetection.Detect(Multipart("encrypted", Part("application", "pgp-encrypted"), Part("application", "octet-stream"))), Is.EqualTo(MailEncryptionKind.PGP_MIME));
            Assert.That(MailEncryptionDetection.Detect(Multipart("mixed", PLAIN_TEXT, Part("application", "x-microsoft-rpmsg-message"))), Is.EqualTo(MailEncryptionKind.MICROSOFT_IRM));
            Assert.That(MailEncryptionDetection.Detect(Multipart("mixed", PLAIN_TEXT, Part("application", "octet-stream", name: "message.rpmsg"))), Is.EqualTo(MailEncryptionKind.MICROSOFT_IRM), "The IRM attachment is only known by its type.");
        });
    }

    [Test]
    public void TheStructureFromTheServerTellsAReadableMail()
    {
        var attachedEncryptedMail = new BodyPartMessage(new ContentType("message", "rfc822"), PART_SPECIFIER)
        {
            Body = Part("application", "pkcs7-mime", smimeType: "enveloped-data"),
        };

        Assert.Multiple(() =>
        {
            Assert.That(MailEncryptionDetection.Detect((BodyPart?)null), Is.EqualTo(MailEncryptionKind.NONE));
            Assert.That(MailEncryptionDetection.Detect(Multipart("alternative", PLAIN_TEXT, Part("text", "html"))), Is.EqualTo(MailEncryptionKind.NONE));
            Assert.That(MailEncryptionDetection.Detect(Multipart("signed", PLAIN_TEXT, Part("application", "pkcs7-signature", name: "smime.p7s"))), Is.EqualTo(MailEncryptionKind.NONE), "A mail signed in the clear counts as encrypted.");
            Assert.That(MailEncryptionDetection.Detect(Multipart("mixed", PLAIN_TEXT, attachedEncryptedMail)), Is.EqualTo(MailEncryptionKind.NONE), "An attached encrypted mail makes the mail around it unreadable.");
        });
    }

    [TestCase("-----BEGIN PGP MESSAGE-----\n\nhQEMA\n-----END PGP MESSAGE-----", ExpectedResult = true)]
    [TestCase("<p>-----BEGIN PGP MESSAGE-----<br>hQEMA</p>", ExpectedResult = true)]
    [TestCase("-----BEGIN PGP SIGNED MESSAGE-----\nHash: SHA256\n\nReadable text.", ExpectedResult = false)]
    [TestCase("Nothing to see here.", ExpectedResult = false)]
    [TestCase(null, ExpectedResult = false)]
    public bool AnEncryptedPgpBlockIsFoundInTheText(string? text) => MailEncryptionDetection.ContainsInlinePgpMessage(text);

    private static BodyPartBasic Part(string mediaType, string mediaSubtype, string? smimeType = null, string? name = null)
    {
        var contentType = new ContentType(mediaType, mediaSubtype);
        if (smimeType is not null)
            contentType.Parameters.Add("smime-type", smimeType);

        if (name is not null)
            contentType.Name = name;

        return new BodyPartBasic(contentType, PART_SPECIFIER);
    }

    private static BodyPartMultipart Multipart(string mediaSubtype, params BodyPart[] parts)
    {
        var multipart = new BodyPartMultipart(new ContentType("multipart", mediaSubtype), PART_SPECIFIER);
        foreach (var part in parts)
            multipart.BodyParts.Add(part);

        return multipart;
    }
}