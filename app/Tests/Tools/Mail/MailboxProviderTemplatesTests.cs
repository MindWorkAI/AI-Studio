using AIStudio.Settings.DataModel;
using AIStudio.Tools.Mail;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks that every provider template would make a mailbox the connector accepts.
/// </summary>
/// <remarks>
/// The table is typed in by hand. A host with a stray scheme or a port out of range would only
/// show itself once somebody picks that provider, and then as a failed connection the user cannot
/// explain.
/// </remarks>
[TestFixture]
public sealed class MailboxProviderTemplatesTests
{
    private static IEnumerable<TestCaseData> Templates() => MailboxProviderTemplates.ALL.Select(template => new TestCaseData(template).SetArgDisplayNames(template.Name));

    [TestCaseSource(nameof(Templates))]
    public void ATemplateMakesAValidConnection(MailboxProviderTemplate template)
    {
        Assert.Multiple(() =>
        {
            Assert.That(template.Port, Is.InRange(1, 65535), "The port is out of range.");
            Assert.That(template.TransportSecurity, Is.Not.EqualTo(MailboxTransportSecurity.UNKNOWN), "The template does not say how the connection is encrypted.");
            Assert.That(Uri.TryCreate(template.HelpUrl, UriKind.Absolute, out var helpUrl) && helpUrl.Scheme == Uri.UriSchemeHttps, Is.True, "The help page is no HTTPS address.");
        });
    }

    [TestCaseSource(nameof(Templates))]
    public void ATemplateNamesAHostUnlessEveryOrganizationRunsItsOwn(MailboxProviderTemplate template)
    {
        if (template.Requirements.HasFlag(MailboxProviderRequirements.ADMIN_ACTIVATION))
            Assert.That(template.Host, Is.Empty, "A server every organization runs itself has no host to fill in.");
        else
            Assert.That(Uri.CheckHostName(template.Host), Is.EqualTo(UriHostNameType.Dns), "The host is no DNS name.");
    }

    [Test]
    public void EveryProviderIsListedOnce()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MailboxProviderTemplates.ALL.Select(template => template.Name), Is.Unique);
            Assert.That(MailboxProviderTemplates.ALL.Where(template => template.Host.Length > 0).Select(template => template.Host), Is.Unique);
        });
    }

    [Test]
    public void EveryRequirementHasItsOwnDescription()
    {
        var requirements = Enum.GetValues<MailboxProviderRequirements>().Where(requirement => requirement is not MailboxProviderRequirements.NONE).ToList();
        var descriptions = requirements.Select(requirement => requirement.GetDescriptions().ToList()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(MailboxProviderRequirements.NONE.GetDescriptions(), Is.Empty, "A provider without requirements got a description.");
            Assert.That(descriptions, Has.All.Count.EqualTo(1), "A requirement does not have exactly one description.");
            Assert.That(descriptions.Select(texts => texts.FirstOrDefault()), Is.Unique, "Two requirements share a description.");
        });
    }

    [Test]
    public void EveryUsernameFormatHasItsOwnDescription()
    {
        var descriptions = Enum.GetValues<MailboxUsernameFormat>().Select(format => format.GetDescription()).ToList();
        var fallback = ((MailboxUsernameFormat)int.MaxValue).GetDescription();

        Assert.Multiple(() =>
        {
            Assert.That(descriptions, Is.Unique, "Two username formats share a description.");
            Assert.That(descriptions, Has.None.EqualTo(fallback), "A username format falls back to the description of an unknown one.");
        });
    }
}