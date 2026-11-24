using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which mail servers AI Studio may connect to, and in which encoding a mailbox signs in.
/// </summary>
/// <remarks>
/// Without a rule of the organization, every server is allowed, as before. With it, only the hosts
/// of the mail servers the organization offers are, however they are written. An organization
/// which switched the rule on but offers no mail server yet allows none at all, rather than all.
/// The encoding the organization names for a server wins over the one of the mailbox, with or
/// without the rule: it also reaches mailboxes added before the organization named it.
/// </remarks>
[TestFixture]
public sealed class MailServerPolicyTests
{
    private static readonly MailServerPolicy ORGANIZATION_ONLY = new(true, [new("imap.intra.example.org", null), new("mail.müller.example", null)]);

    private static readonly MailServerPolicy WITH_ENCODINGS = new(false, [new("imap.intra.example.org", null), new("exchange.müller.example", "windows-1254")]);

    [TestCase("imap.example.com")]
    [TestCase("imap.gmail.com")]
    public void WithoutARuleEveryServerIsAllowed(string host) => Assert.That(MailServerPolicy.ANY_SERVER.IsAllowed(host), Is.True);

    [TestCase("imap.intra.example.org")]
    [TestCase("IMAP.Intra.Example.org")]
    [TestCase("mail.xn--mller-kva.example")]
    public void TheServersOfTheOrganizationAreAllowed(string host) => Assert.That(ORGANIZATION_ONLY.IsAllowed(host), Is.True);

    [TestCase("imap.gmail.com")]
    [TestCase("intra.example.org")]
    [TestCase("imap.intra.example.org.evil.example")]
    [TestCase("")]
    public void EveryOtherServerIsRefused(string host) => Assert.That(ORGANIZATION_ONLY.IsAllowed(host), Is.False);

    [Test]
    public void ARuleWithoutServersAllowsNone() => Assert.That(new MailServerPolicy(true, []).IsAllowed("imap.intra.example.org"), Is.False);

    [TestCase("exchange.müller.example", ExpectedResult = "windows-1254")]
    [TestCase("EXCHANGE.xn--mller-kva.example", ExpectedResult = "windows-1254")]
    [TestCase("imap.intra.example.org", ExpectedResult = null)]
    [TestCase("imap.gmail.com", ExpectedResult = null)]
    public string? TheOrganizationNamesTheEncodingOfItsServers(string host) => WITH_ENCODINGS.GetOrganizationPasswordEncoding(host);

    [TestCase("exchange.müller.example", "utf-8", ExpectedResult = "windows-1254")]
    [TestCase("exchange.müller.example", "x-encoding-of-the-future", ExpectedResult = "windows-1254")]
    [TestCase("imap.intra.example.org", "windows-1252", ExpectedResult = "windows-1252")]
    [TestCase("imap.gmail.com", "utf-8", ExpectedResult = "utf-8")]
    public string AMailboxSignsInWithTheEncodingOfTheOrganizationFirst(string host, string mailboxEncoding) => WITH_ENCODINGS.GetPasswordEncoding(new DataSourceMailbox { Host = host, PasswordEncoding = mailboxEncoding });

    [Test]
    public void WithoutAnOrganizationAMailboxKeepsItsEncoding() => Assert.That(MailServerPolicy.ANY_SERVER.GetPasswordEncoding(new DataSourceMailbox { Host = "imap.gmail.com", PasswordEncoding = "iso-8859-15" }), Is.EqualTo("iso-8859-15"));
}