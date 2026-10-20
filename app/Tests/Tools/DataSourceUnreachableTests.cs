using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which ERI data sources whose server could not be reached may still be named to a provider.
/// </summary>
/// <remarks>
/// Such a data source is never searched, but Semantic Search tells the model about it, so that the
/// model does not take its silence for finding nothing. Its server's requirements are unknown then,
/// so only the security policy configured in AI Studio decides, and a provider which that policy
/// keeps away must not even learn the name.
/// </remarks>
[TestFixture]
public sealed class DataSourceUnreachableTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void ADataSourceForAnyProviderMayBeNamed(bool usingTrustedProvider)
    {
        Assert.That(DataSourceService.MayBeNamedWhileUnreachable(ERISource(DataSourceSecurity.ALLOW_ANY), usingTrustedProvider), Is.True);
    }

    [Test]
    public void ADataSourceForSelfHostedProvidersIsNamedOnlyToATrustedOne()
    {
        var source = ERISource(DataSourceSecurity.SELF_HOSTED);

        Assert.Multiple(() =>
        {
            Assert.That(DataSourceService.MayBeNamedWhileUnreachable(source, usingTrustedProvider: true), Is.True);
            Assert.That(DataSourceService.MayBeNamedWhileUnreachable(source, usingTrustedProvider: false), Is.False, "The data source would be refused to this provider if its server answered.");
        });
    }

    [Test]
    public void ADataSourceWithoutASecurityPolicyIsNeverNamed()
    {
        Assert.That(DataSourceService.MayBeNamedWhileUnreachable(ERISource(DataSourceSecurity.NOT_SPECIFIED), usingTrustedProvider: true), Is.False, "Better safe than sorry, as with a server which answers.");
    }

    private static DataSourceERI_V1 ERISource(DataSourceSecurity securityPolicy) => new()
    {
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Intranet",
        Type = DataSourceType.ERI_V1,
        SecurityPolicy = securityPolicy,
    };
}