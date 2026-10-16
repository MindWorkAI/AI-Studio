using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks which mail servers AI Studio may connect to.
/// </summary>
/// <remarks>
/// Without a rule of the organization, every server is allowed, as before. With it, only the hosts
/// of the mail servers the organization offers are, however they are written. An organization
/// which switched the rule on but offers no mail server yet allows none at all, rather than all.
/// </remarks>
[TestFixture]
public sealed class MailServerPolicyTests
{
    private static readonly MailServerPolicy ORGANIZATION_ONLY = new(true, ["imap.intra.example.org", "mail.müller.example"]);

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
}