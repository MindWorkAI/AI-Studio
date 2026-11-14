using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks which models a new preset or comparison series of the Model Comparison assistant starts with.
/// </summary>
/// <remarks>
/// Every test configures at least two providers: with exactly one, GetPreselectedProvider takes a shortcut and
/// returns it without looking at any preselection, so a test of the fallback would pass for the wrong reason.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonPreselectionTests
{
    private const string DAILY_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string JUDGE_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string APP_DEFAULT_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";

    [Test]
    public void WithoutOptionsTheFirstModelIsTheAppDefaultAndThereIsNoJudge()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.ModelComparison.PreselectedProvider = DAILY_PROVIDER_ID;
        settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider = JUDGE_PROVIDER_ID;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.PreselectedComparisonModel.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "With the options switched off, the stored models must not count.");
            Assert.That(settingsManager.PreselectedComparisonJudge, Is.EqualTo(AIStudio.Settings.Provider.NONE), "Without the options, a new comparison starts without a judge.");
        }
    }

    [Test]
    public void TheDefaultOptionsPreselectNoModel()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.ConfigurationData.ModelComparison.PreselectedProvider, Is.EqualTo(DataModelComparison.NO_MODEL));
            Assert.That(settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider, Is.EqualTo(DataModelComparison.NO_MODEL));
            Assert.That(settingsManager.PreselectedComparisonModel, Is.EqualTo(AIStudio.Settings.Provider.NONE), "\"No model preselected\" must not fall back to the provider of the whole app.");
            Assert.That(settingsManager.PreselectedComparisonJudge, Is.EqualTo(AIStudio.Settings.Provider.NONE));
        }
    }

    [Test]
    public void AnEmptyOptionUsesTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;
        settingsManager.ConfigurationData.ModelComparison.PreselectedProvider = DAILY_PROVIDER_ID;
        settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider = string.Empty;

        Assert.That(settingsManager.PreselectedComparisonJudge.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "\"Use app default\" means the provider of the whole app, not the first model of the comparison.");
    }

    [Test]
    public void TheOptionsPreselectTheirModels()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;
        settingsManager.ConfigurationData.ModelComparison.PreselectedProvider = DAILY_PROVIDER_ID;
        settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider = JUDGE_PROVIDER_ID;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.PreselectedComparisonModel.Id, Is.EqualTo(DAILY_PROVIDER_ID));
            Assert.That(settingsManager.PreselectedComparisonJudge.Id, Is.EqualTo(JUDGE_PROVIDER_ID));
        }
    }

    /// <summary>
    /// Checks that a preselected model which misses the confidence requirements of the assistant preselects none.
    /// </summary>
    /// <remarks>
    /// Under the default scheme, every provider reaches MEDIUM and a self-hosted one HIGH, so a minimum of HIGH
    /// separates the two.
    /// </remarks>
    [Test]
    public void AModelBelowTheMinimumConfidenceIsNotPreselected()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, "44444444-4444-4444-4444-444444444444", LLMProviders.SELF_HOSTED);
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;
        settingsManager.ConfigurationData.ModelComparison.MinimumProviderConfidence = ConfidenceLevel.HIGH;
        settingsManager.ConfigurationData.ModelComparison.PreselectedProvider = DAILY_PROVIDER_ID;
        settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider = "44444444-4444-4444-4444-444444444444";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.PreselectedComparisonModel, Is.EqualTo(AIStudio.Settings.Provider.NONE), "A model the assistant may not use must not start a comparison.");
            Assert.That(settingsManager.PreselectedComparisonJudge.Id, Is.EqualTo("44444444-4444-4444-4444-444444444444"), "A model which meets the requirements stays preselected.");
        }
    }

    [Test]
    public void ADeletedModelIsNotPreselected()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;
        settingsManager.ConfigurationData.ModelComparison.PreselectedJudgeProvider = "55555555-5555-5555-5555-555555555555";

        Assert.That(settingsManager.PreselectedComparisonJudge, Is.EqualTo(AIStudio.Settings.Provider.NONE));
    }

    /// <summary>
    /// Builds the settings with three providers, one of them preselected for the whole app.
    /// </summary>
    /// <remarks>
    /// The rust service is handed in as null on purpose: resolving a provider never asks it anything.
    /// </remarks>
    private static SettingsManager CreateSettingsManager()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        AddProvider(settingsManager, DAILY_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, JUDGE_PROVIDER_ID, LLMProviders.ANTHROPIC);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;
        return settingsManager;
    }

    private static void AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null)));
    }
}