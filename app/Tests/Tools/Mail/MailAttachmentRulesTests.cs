using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;
using AIStudio.Tools.Validation;

using MailKit;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which attachments of a mail get their text read.
/// </summary>
/// <remarks>
/// All of it is decided from the structure of the mail, before anything of an attachment is fetched.
/// </remarks>
[TestFixture]
public sealed class MailAttachmentRulesTests
{
    /// <summary>
    /// Where a part sits in the mail, e.g. "1.2". Nothing here looks at it.
    /// </summary>
    private const string PART_SPECIFIER = "2";

    private const long MEGABYTE = 1024L * 1024L;

    [Test]
    public void ADocumentWithinTheLimitIsRead() =>
        Assert.That(MailAttachmentRules.GetReasonToSkip(Attachment("application", "pdf", "board-report.pdf", 2 * MEGABYTE), 10), Is.Null);

    [Test]
    public void AMailboxWhichReadsNoAttachmentsSkipsEveryOne()
    {
        var mailbox = new DataSourceMailbox { IndexAttachments = false, MaxAttachmentSizeMegabytes = 10 };
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.GetMaxSizeMegabytes(mailbox), Is.Null);
            Assert.That(MailAttachmentRules.GetReasonToSkip(Attachment("application", "pdf", "board-report.pdf", 1024), null), Is.EqualTo(MailPartTextState.ATTACHMENTS_DISABLED));
        });
    }

    [Test]
    public void AnythingButADocumentIsKnownByItsNameOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.GetReasonToSkip(Attachment("image", "png", "photo.png", 1024), 10), Is.EqualTo(MailPartTextState.UNSUPPORTED_TYPE));
            Assert.That(MailAttachmentRules.GetReasonToSkip(Attachment("application", "zip", "reports.zip", 1024), 10), Is.EqualTo(MailPartTextState.UNSUPPORTED_TYPE));
            Assert.That(MailAttachmentRules.GetReasonToSkip(Attachment("application", "octet-stream", null, 1024), 10), Is.EqualTo(MailPartTextState.UNSUPPORTED_TYPE));
        });
    }

    [Test]
    public void TheLimitCountsTheFileNotItsEncoding()
    {
        // 12 MB of Base64 decode to 9 MB, which the limit of 10 MB lets through:
        var base64 = Attachment("application", "pdf", "board-report.pdf", 12 * MEGABYTE);
        var unencoded = Attachment("application", "pdf", "board-report.pdf", 12 * MEGABYTE, "binary");

        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.EstimateDecodedSize(base64), Is.EqualTo(9 * MEGABYTE));
            Assert.That(MailAttachmentRules.GetReasonToSkip(base64, 10), Is.Null);
            Assert.That(MailAttachmentRules.GetReasonToSkip(unencoded, 10), Is.EqualTo(MailPartTextState.TOO_LARGE));
        });
    }

    [Test]
    public void AnAttachmentBeyondWhatCanBeFetchedIsTooLargeWhateverTheLimit()
    {
        // 2.5 GB of Base64 decode to less than the largest limit, but cannot be fetched in pieces:
        var attachment = Attachment("application", "pdf", "scanned-archive.pdf", 2560 * MEGABYTE);
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.EstimateDecodedSize(attachment), Is.LessThan(DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES * MEGABYTE));
            Assert.That(MailAttachmentRules.GetReasonToSkip(attachment, DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES), Is.EqualTo(MailPartTextState.TOO_LARGE));
        });
    }

    [Test]
    public void TheTypeComesFromTheNameOrElseFromTheContentType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.GetExtension(Attachment("application", "octet-stream", "Minutes.DOCX", 1024)), Is.EqualTo("docx"));
            Assert.That(MailAttachmentRules.GetExtension(Attachment("application", "pdf", null, 1024)), Is.EqualTo("pdf"));
            Assert.That(MailAttachmentRules.GetExtension(Attachment("application", "pdf", "../../../evil.pdf", 1024)), Is.EqualTo("pdf"), "Something of the name but its type made it into the path of the file.");
            Assert.That(MailAttachmentRules.GetExtension(Attachment("application", "x-unknown", "notes", 1024)), Is.Empty);
        });
    }

    [Test]
    public void ALimitBeyondTheDialogIsBroughtBackIntoRange()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.GetMaxSizeMegabytes(new DataSourceMailbox { MaxAttachmentSizeMegabytes = 25 }), Is.EqualTo(25));
            Assert.That(MailAttachmentRules.GetMaxSizeMegabytes(new DataSourceMailbox { MaxAttachmentSizeMegabytes = 100_000 }), Is.EqualTo(DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES));
            Assert.That(MailAttachmentRules.GetMaxSizeMegabytes(new DataSourceMailbox { MaxAttachmentSizeMegabytes = -1 }), Is.EqualTo(DataSourceValidation.MIN_ATTACHMENT_SIZE_MEGABYTES));
        });
    }

    [Test]
    public void TheSignatureOfASignedMailIsNoAttachment()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailAttachmentRules.IsSignature(new ContentType("application", "pkcs7-signature")), Is.True);
            Assert.That(MailAttachmentRules.IsSignature(new ContentType("application", "x-pkcs7-signature")), Is.True);
            Assert.That(MailAttachmentRules.IsSignature(new ContentType("application", "pgp-signature")), Is.True);
            Assert.That(MailAttachmentRules.IsSignature(new ContentType("application", "pdf")), Is.False);
        });
    }

    private static BodyPartBasic Attachment(string mediaType, string mediaSubtype, string? fileName, long octets, string transferEncoding = "base64") => new(new ContentType(mediaType, mediaSubtype), PART_SPECIFIER)
    {
        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment) { FileName = fileName },
        ContentTransferEncoding = transferEncoding,
        Octets = (uint)octets,
    };
}