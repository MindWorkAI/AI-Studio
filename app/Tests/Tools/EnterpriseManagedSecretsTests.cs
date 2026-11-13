using AIStudio.Provider;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which secrets AI Studio removes from the OS keyring when an organization changes its encryption secret.
/// </summary>
/// <remarks>
/// Only what the configuration of the organization brought along goes, since the next start of the configuration
/// stores it again with the new secret. A key the user entered for a provider which allows it has no such way
/// back: removing it made every user enter the key once more, and the home page asked them all again.
/// </remarks>
[TestFixture]
public sealed class EnterpriseManagedSecretsTests
{
    private static readonly Guid PLUGIN_ID = new("44444444-4444-4444-4444-444444444444");

    [Test]
    public void TheKeyOfTheOrganizationGoes()
    {
        Assert.That(EnterpriseEnvironmentService.HoldsSecretOfTheOrganization(ProviderOfTheOrganization()), Is.True);
    }

    [Test]
    public void TheKeyTheUserEnteredStays()
    {
        Assert.That(EnterpriseEnvironmentService.HoldsSecretOfTheOrganization(ProviderOfTheOrganization() with { AllowUserProvidedAPIKey = true }), Is.False);
    }

    [Test]
    public void TheKeyTheHomePageAskedForStays()
    {
        Assert.That(EnterpriseEnvironmentService.HoldsSecretOfTheOrganization(ProviderOfTheOrganization() with { AllowUserProvidedAPIKey = true, PromptForUserAPIKey = true }), Is.False);
    }

    [Test]
    public void AProviderOfTheUsersOwnIsNoConcernOfTheOrganization()
    {
        Assert.That(EnterpriseEnvironmentService.HoldsSecretOfTheOrganization(new AIStudio.Settings.Provider(1, Guid.NewGuid().ToString(), "My own provider", LLMProviders.OPEN_AI, new Model("gpt-6", "GPT-6"))), Is.False);
    }

    private static AIStudio.Settings.Provider ProviderOfTheOrganization() => new(1, Guid.NewGuid().ToString(), "Provider of the organization", LLMProviders.OPEN_AI, new Model("gpt-6", "GPT-6"), IsEnterpriseConfiguration: true, EnterpriseConfigurationPluginId: PLUGIN_ID);
}