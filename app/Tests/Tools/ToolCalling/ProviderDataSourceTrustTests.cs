using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that a provider is trusted with a data source the same way in both forms a request knows it by.
/// </summary>
/// <remarks>
/// The classic RAG process asks the provider instance whether it may use a data source which asks
/// for a self-hosted or organization-trusted provider. Semantic Search decides which data sources
/// it offers with the settings the instance creates for the request. When the two answer
/// differently, a self-hosted provider searches such a data source in one process and does not even
/// learn about it in the other.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ProviderDataSourceTrustTests : ToolRegistryTestBase
{
    [Test]
    public void ASelfHostedProviderIsTrustedInBothForms()
    {
        var (instance, settings) = BothForms(ToolCapableProvider());

        Assert.Multiple(() =>
        {
            Assert.That(instance.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.True);
            Assert.That(settings.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.True, "Semantic Search would otherwise not offer a data source for self-hosted providers.");
        });
    }

    [Test]
    public void AProviderTheOrganizationTrustsIsTrustedInBothForms()
    {
        var cloud = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.DataSourceSecurity.TrustedProviderIds.Add(cloud.Id);
        var (instance, settings) = BothForms(cloud);

        Assert.Multiple(() =>
        {
            Assert.That(instance.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.True);
            Assert.That(settings.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.True, "The organization trusts the provider by its ID, so the settings have to keep it.");
        });
    }

    [Test]
    public void AnUntrustedProviderStaysUntrustedInBothForms()
    {
        var (instance, settings) = BothForms(LessTrustedProvider());

        Assert.Multiple(() =>
        {
            Assert.That(instance.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.False);
            Assert.That(settings.IsTrustedForDataSourceSecurityChecks(this.SettingsManager), Is.False);
        });
    }

    private static (IProvider Instance, AIStudio.Settings.Provider Settings) BothForms(AIStudio.Settings.Provider configured)
    {
        var instance = configured.CreateProvider();
        return (instance, instance.CreateSettingsProvider(configured.Model));
    }
}