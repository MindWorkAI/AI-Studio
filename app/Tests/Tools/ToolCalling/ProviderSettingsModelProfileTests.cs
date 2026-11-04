using AIStudio.Models;
using AIStudio.Models.Live;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that the settings a provider creates for a request know its model as the configured provider does.
/// </summary>
/// <remarks>
/// The settings of a request carry the ID of the configured provider, see
/// IProvider.CreateSettingsProvider. Through it, what the provider's own model list reported, such as
/// the context window of a self-hosted model, reaches everything which asks the settings about the
/// model: the tools offered in the request, and how images are passed on. Before the ID came along,
/// the request knew the model only by the rules, while the provider dialog showed what the machine
/// reported.<br/><br/>
/// What a provider reported lands in the store the app shares, so these tests must not run next to
/// anything else touching it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ProviderSettingsModelProfileTests : ToolRegistryTestBase
{
    private const string MACHINE = "55555555-5555-5555-5555-555555555555";

    /// <summary>
    /// A window no rule would ever state, so that finding it proves where the answer came from.
    /// </summary>
    private const int WHAT_THE_MACHINE_REPORTS = 55_555;

    private static readonly Model MODEL = new("qwen3-32b", null);

    [TearDown]
    public void ForgetWhatTheMachineSaid() => ListedModels.Shared.Report(MACHINE, []);

    [Test]
    public void TheSettingsOfARequestKnowWhatTheMachineReported()
    {
        ListedModels.Shared.Report(MACHINE, [new(MODEL.Id, ContextWindow.Of(WHAT_THE_MACHINE_REPORTS))]);
        var configured = new AIStudio.Settings.Provider(1, MACHINE, "A machine of my own", LLMProviders.SELF_HOSTED, MODEL, IsSelfHosted: true);
        var settings = configured.CreateProvider().CreateSettingsProvider(MODEL);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.GetModelProfile().Context.DefaultTokens, Is.EqualTo(WHAT_THE_MACHINE_REPORTS));
            Assert.That(settings.GetModelProfile(), Is.EqualTo(configured.GetModelProfile()), "A request has to know the model as the configured provider does.");
        }
    }
}