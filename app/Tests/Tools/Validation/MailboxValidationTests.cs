using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks what the mailbox dialog accepts before it lets the user test a connection or save.
/// </summary>
[TestFixture]
public sealed class MailboxValidationTests
{
    private static readonly DataSourceValidation VALIDATION = new();

    [TestCase("imap.example.org", ExpectedResult = true)]
    [TestCase("  imap.example.org  ", ExpectedResult = true)]
    [TestCase("posteo.de", ExpectedResult = true)]
    [TestCase("192.168.1.20", ExpectedResult = true)]
    [TestCase("::1", ExpectedResult = true)]
    [TestCase("", ExpectedResult = false)]
    [TestCase("   ", ExpectedResult = false)]
    [TestCase("imaps://imap.example.org", ExpectedResult = false)]
    [TestCase("imap.example.org:993", ExpectedResult = false)]
    [TestCase("imap.example.org/INBOX", ExpectedResult = false)]
    public bool TheHostStandsAlone(string host) => DataSourceValidation.ValidateMailboxHost(host) is null;

    [Test]
    public void AnUnknownEncryptionIsRejectedAndNeverOffered()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DataSourceValidation.ValidateMailboxTransportSecurity(MailboxTransportSecurity.UNKNOWN), Is.Not.Null);
            Assert.That(ConfigurationSelectDataFactory.GetMailboxTransportSecurityData().Select(option => option.Value), Is.EqualTo(new[] { MailboxTransportSecurity.SSL_ON_CONNECT, MailboxTransportSecurity.STARTTLS }));
        });
    }

    [TestCase(MailboxTransportSecurity.SSL_ON_CONNECT, ExpectedResult = 993)]
    [TestCase(MailboxTransportSecurity.STARTTLS, ExpectedResult = 143)]
    [TestCase(MailboxTransportSecurity.UNKNOWN, ExpectedResult = null)]
    public int? EachEncryptionComesWithItsUsualPort(MailboxTransportSecurity transportSecurity) => transportSecurity.GetUsualPort();

    [TestCase(0, ExpectedResult = false)]
    [TestCase(DataSourceValidation.MIN_ATTACHMENT_SIZE_MEGABYTES, ExpectedResult = true)]
    [TestCase(DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES, ExpectedResult = true)]
    [TestCase(DataSourceValidation.MAX_ATTACHMENT_SIZE_MEGABYTES + 1, ExpectedResult = false)]
    public bool TheAttachmentSizeStaysInItsRange(int megabytes) => DataSourceValidation.ValidateMailboxMaxAttachmentSize(megabytes) is null;

    [Test]
    public void APasswordIsRequiredAndAStorageIssueComesFirst()
    {
        const string STORAGE_ISSUE = "The keyring is locked.";
        var validationWithIssue = new DataSourceValidation { GetSecretStorageIssue = () => STORAGE_ISSUE };

        Assert.Multiple(() =>
        {
            Assert.That(VALIDATION.ValidateMailboxPassword(string.Empty), Is.Not.Null, "An empty password was accepted.");
            Assert.That(VALIDATION.ValidateMailboxPassword(" secret with spaces "), Is.Null, "A password was trimmed or rejected for its spaces.");
            Assert.That(validationWithIssue.ValidateMailboxPassword("secret"), Is.EqualTo(STORAGE_ISSUE), "The issue of the keyring was not shown.");
        });
    }
}