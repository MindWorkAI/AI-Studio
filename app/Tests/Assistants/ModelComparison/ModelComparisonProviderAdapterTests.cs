using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks that the model comparison asks a provider only when every chat of the app would ask it.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because the check of a chat
/// reads them from there, see ProhibitedProvidersTests.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonProviderAdapterTests
{
    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task AProviderWhichTheOrganizationProhibitedIsNotAsked()
    {
        var provider = new AIStudio.Settings.Provider(1, Guid.NewGuid().ToString(), "Instance 1", LLMProviders.DEEP_SEEK, new("test-model", null));
        var participant = ModelComparisonProviderAdapter.CreateParticipant(provider, this.settingsManager, AIStudio.Tools.Components.MODEL_COMPARISON_ASSISTANT, "Model Comparison");

        // Prohibited after it was selected, as an organization may do at any time:
        this.settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.DEEP_SEEK);

        // Thrown before any request leaves the app; the runner turns it into a model which did not answer:
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in participant.Ask("A question.", CancellationToken.None))
            {
            }
        });
    }
}