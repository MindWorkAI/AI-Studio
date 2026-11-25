using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks what a new comparison series takes over from an existing one, and what it leaves out.
/// </summary>
[TestFixture]
public sealed class ModelComparisonSeriesCopyTests
{
    private const string FIRST_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string SECOND_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string JUDGE_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";
    private const string PROFILE_ID = "44444444-4444-4444-4444-444444444444";
    private const string DATA_SOURCE_ID = "55555555-5555-5555-5555-555555555555";
    private const string OTHER_DATA_SOURCE_ID = "66666666-6666-6666-6666-666666666666";
    private const string TOOL_ID = "web_search";
    private const string OTHER_TOOL_ID = "read_web_page";

    [Test]
    public void AnAvailableSetupIsTakenOverCompletely()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.First.Id, Is.EqualTo(FIRST_PROVIDER_ID));
            Assert.That(copy.Second.Id, Is.EqualTo(SECOND_PROVIDER_ID));
            Assert.That(copy.JudgeEnabled, Is.True);
            Assert.That(copy.Judge.Id, Is.EqualTo(JUDGE_PROVIDER_ID));
            Assert.That(copy.Profile.Id, Is.EqualTo(PROFILE_ID));
            Assert.That(copy.ToolIds, Is.EquivalentTo(new[] { TOOL_ID, OTHER_TOOL_ID }));
            Assert.That(copy.DataSourceOptions.DisableDataSources, Is.False);
            Assert.That(copy.DataSourceOptions.PreselectedDataSourceIds, Is.EquivalentTo(new[] { DATA_SOURCE_ID, OTHER_DATA_SOURCE_ID }));
            Assert.That(copy.DataSourceOptions.RetrievalMode, Is.EqualTo(DataSourceRetrievalMode.EVERY_MESSAGE), "Both chats of the new series must search the same way as before.");
            Assert.That(copy.DataSourceOptions.AutomaticDataSourceSelection, Is.False, "An AI never selects the data sources of a series.");
            Assert.That(copy.DataSourceOptions.AutomaticValidation, Is.False, "An AI never validates what the data sources of a series gave.");
            Assert.That(copy.MissingParts, Is.Empty);
        }
    }

    /// <summary>
    /// Checks that a model which is gone or which is another one now has to be chosen anew.
    /// </summary>
    /// <remarks>
    /// A provider whose model changed would otherwise carry the old comparison over to a model the user never chose for
    /// it.
    /// </remarks>
    [Test]
    public void ADeletedOrChangedModelIsLeftOut()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);
        var providers = settingsManager.ConfigurationData.Providers;
        providers.RemoveAll(provider => provider.Id == FIRST_PROVIDER_ID);
        var second = providers.Single(provider => provider.Id == SECOND_PROVIDER_ID);
        providers[providers.IndexOf(second)] = second with { Model = new("another-model", null) };

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.First, Is.EqualTo(AIStudio.Settings.Provider.NONE));
            Assert.That(copy.Second, Is.EqualTo(AIStudio.Settings.Provider.NONE));
            Assert.That(copy.MissingParts, Is.EquivalentTo(new[] { ModelComparisonSeriesPart.FIRST_MODEL, ModelComparisonSeriesPart.SECOND_MODEL }));
        }
    }

    /// <summary>
    /// Checks that a model which the assistant may no longer use has to be chosen anew.
    /// </summary>
    /// <remarks>
    /// Under the default scheme, every provider reaches MEDIUM and a self-hosted one HIGH, so a minimum of HIGH leaves
    /// out the two compared models, but not the judge.
    /// </remarks>
    [Test]
    public void AModelBelowTheMinimumConfidenceIsLeftOut()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);
        settingsManager.ConfigurationData.ModelComparison.PreselectOptions = true;
        settingsManager.ConfigurationData.ModelComparison.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Judge.Id, Is.EqualTo(JUDGE_PROVIDER_ID));
            Assert.That(copy.MissingParts, Is.EquivalentTo(new[] { ModelComparisonSeriesPart.FIRST_MODEL, ModelComparisonSeriesPart.SECOND_MODEL }));
        }
    }

    /// <summary>
    /// Checks that the judge stays switched on when its model is gone: the user wanted a judge, and chooses another one.
    /// </summary>
    [Test]
    public void AMissingJudgeKeepsTheJudgeSwitchedOn()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);
        settingsManager.ConfigurationData.Providers.RemoveAll(provider => provider.Id == JUDGE_PROVIDER_ID);

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.JudgeEnabled, Is.True);
            Assert.That(copy.Judge, Is.EqualTo(AIStudio.Settings.Provider.NONE));
            Assert.That(copy.MissingParts, Is.EquivalentTo(new[] { ModelComparisonSeriesPart.JUDGE }));
        }
    }

    [Test]
    public void ASeriesWithoutJudgeStartsWithoutJudge()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager) with { JudgeProviderId = string.Empty, Judge = null };

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.JudgeEnabled, Is.False);
            Assert.That(copy.Judge, Is.EqualTo(AIStudio.Settings.Provider.NONE));
            Assert.That(copy.MissingParts, Is.Empty, "A series without a judge misses nothing.");
        }
    }

    [Test]
    public void AMissingProfileToolAndDataSourceAreLeftOut()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);
        settingsManager.ConfigurationData.Profiles.Clear();
        settingsManager.ConfigurationData.DataSources.RemoveAll(dataSource => dataSource.Id == OTHER_DATA_SOURCE_ID);

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Profile, Is.EqualTo(Profile.NO_PROFILE));
            Assert.That(copy.ToolIds, Is.EquivalentTo(new[] { TOOL_ID }), "The tools which are still available stay selected.");
            Assert.That(copy.DataSourceOptions.PreselectedDataSourceIds, Is.EquivalentTo(new[] { DATA_SOURCE_ID }), "The data sources which still exist stay selected.");
            Assert.That(copy.MissingParts, Is.EquivalentTo(new[] { ModelComparisonSeriesPart.PROFILE, ModelComparisonSeriesPart.TOOLS, ModelComparisonSeriesPart.DATA_SOURCES }));
        }
    }

    [Test]
    public void ASeriesWithoutProfileToolsAndDataSourcesMissesNothing()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager) with { ProfileId = Profile.NO_PROFILE.Id, ToolIds = [], DataSourceIds = [], RetrievalMode = null };

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Profile, Is.EqualTo(Profile.NO_PROFILE));
            Assert.That(copy.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(copy.DataSourceOptions.RetrievalMode, Is.EqualTo(DataSourceRetrievalMode.SEMANTIC_SEARCH), "Without data sources, the new series offers Semantic Search first, as every new series does.");
            Assert.That(copy.MissingParts, Is.Empty);
        }
    }

    /// <summary>
    /// Checks that the data sources are left out while their preview is switched off, since the dialog cannot show them
    /// then.
    /// </summary>
    [Test]
    public void DataSourcesAreLeftOutWhileTheirPreviewIsSwitchedOff()
    {
        var settingsManager = CreateSettingsManager();
        var series = CreateSeries(settingsManager);
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Clear();

        var copy = ModelComparisonSeriesCopy.Of(settingsManager, series, new HashSet<string> { TOOL_ID, OTHER_TOOL_ID });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(copy.DataSourceOptions.PreselectedDataSourceIds, Is.Empty);
            Assert.That(copy.MissingParts, Is.EquivalentTo(new[] { ModelComparisonSeriesPart.DATA_SOURCES }));
        }
    }

    /// <summary>
    /// Builds a series whose whole setup exists in the settings.
    /// </summary>
    private static ModelComparisonSeries CreateSeries(SettingsManager settingsManager) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Weekly reports",
        CreatedAtUtc = DateTimeOffset.UtcNow,
        FirstProviderId = FIRST_PROVIDER_ID,
        SecondProviderId = SECOND_PROVIDER_ID,
        First = ModelComparisonModelIdentity.Of(settingsManager.GetProviderById(FIRST_PROVIDER_ID)),
        Second = ModelComparisonModelIdentity.Of(settingsManager.GetProviderById(SECOND_PROVIDER_ID)),
        JudgeProviderId = JUDGE_PROVIDER_ID,
        Judge = ModelComparisonModelIdentity.Of(settingsManager.GetProviderById(JUDGE_PROVIDER_ID)),
        ProfileId = PROFILE_ID,
        ToolIds = [TOOL_ID, OTHER_TOOL_ID],
        DataSourceIds = [DATA_SOURCE_ID, OTHER_DATA_SOURCE_ID],
        RetrievalMode = DataSourceRetrievalMode.EVERY_MESSAGE,
    };

    /// <summary>
    /// Builds the settings with three providers, a profile, and two data sources, with the preview of data sources
    /// switched on.
    /// </summary>
    /// <remarks>
    /// The rust service is handed in as null on purpose: resolving a provider never asks it anything.
    /// </remarks>
    private static SettingsManager CreateSettingsManager()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        AddProvider(settingsManager, FIRST_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, SECOND_PROVIDER_ID, LLMProviders.MISTRAL);
        AddProvider(settingsManager, JUDGE_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        settingsManager.ConfigurationData.Profiles.Add(new(1, PROFILE_ID, "Reports", "I write reports.", string.Empty));
        settingsManager.ConfigurationData.DataSources.Add(new DataSourceLocalDirectory { Id = DATA_SOURCE_ID, Name = "Reports" });
        settingsManager.ConfigurationData.DataSources.Add(new DataSourceLocalDirectory { Id = OTHER_DATA_SOURCE_ID, Name = "Minutes" });
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_RAG_2024);
        return settingsManager;
    }

    private static void AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null)));
    }
}