using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks when two hosts count as the same mail server.
/// </summary>
/// <remarks>
/// The mailbox dialog recognizes a mail server of the organization by its host, and an
/// organization which allows only its own mail servers decides by it whether AI Studio may connect.
/// A host written another way must not look like a different server, and a different server must
/// never pass for one of the organization.
/// </remarks>
[TestFixture]
public sealed class MailServerHostsTests
{
    [TestCase("imap.example.org", "imap.example.org")]
    [TestCase("imap.example.org", "IMAP.Example.ORG")]
    [TestCase("imap.example.org", " imap.example.org ")]
    [TestCase("imap.example.org", "imap.example.org.")]
    [TestCase("imap.müller.example", "imap.xn--mller-kva.example")]
    [TestCase("192.0.2.10", "192.0.2.10")]
    public void HostsWrittenDifferentlyNameTheSameServer(string host, string otherHost) => Assert.That(MailServerHosts.AreSame(host, otherHost), Is.True);

    [TestCase("imap.example.org", "imap.example.com")]
    [TestCase("imap.example.org", "mail.imap.example.org")]
    [TestCase("imap.example.org", "imap.example.org.evil.example")]
    [TestCase("imap.mueller.example", "imap.müller.example")]
    [TestCase("192.0.2.10", "192.0.2.11")]
    public void DifferentServersStayDifferent(string host, string otherHost) => Assert.That(MailServerHosts.AreSame(host, otherHost), Is.False);

    [TestCase("", "")]
    [TestCase("imaps://imap.example.org", "imaps://imap.example.org")]
    [TestCase("imap.example.org:993", "imap.example.org:993")]
    public void WhatIsNoHostIsNoServer(string host, string otherHost) => Assert.That(MailServerHosts.AreSame(host, otherHost), Is.False, "Two equal texts which are no hosts must not count as a match, or an empty host would match an empty one.");
}