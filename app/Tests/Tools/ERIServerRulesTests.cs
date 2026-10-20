using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which providers the server of an ERI data source allows to see its data.
/// </summary>
/// <remarks>
/// The operator of the server owns the data, so the rule of the server applies on top of the
/// security policy configured in AI Studio, and to a trusted provider as well. Until now, a trusted
/// provider skipped the rule of the server altogether, so a server which allows no provider at all
/// stayed open to it.
/// </remarks>
[TestFixture]
public sealed class ERIServerRulesTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void AServerForAnyProviderAllowsEveryProvider(bool usingTrustedProvider)
    {
        Assert.That(DataSourceService.ServerAllows(new SecurityRequirements(ProviderType.ANY), usingTrustedProvider), Is.True);
    }

    [Test]
    public void AServerForSelfHostedProvidersAllowsOnlyATrustedOne()
    {
        var requirements = new SecurityRequirements(ProviderType.SELF_HOSTED);

        Assert.Multiple(() =>
        {
            Assert.That(DataSourceService.ServerAllows(requirements, usingTrustedProvider: true), Is.True);
            Assert.That(DataSourceService.ServerAllows(requirements, usingTrustedProvider: false), Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AServerWithoutAllowedProvidersAllowsNone(bool usingTrustedProvider)
    {
        Assert.That(DataSourceService.ServerAllows(new SecurityRequirements(ProviderType.NONE), usingTrustedProvider), Is.False, "The operator closed the data source, and a trusted provider is no exception.");
    }

    [Test]
    public void AnUnknownRuleAllowsNoProvider()
    {
        Assert.That(DataSourceService.ServerAllows(new SecurityRequirements((ProviderType)99), usingTrustedProvider: true), Is.False, "Better safe than sorry.");
    }
}