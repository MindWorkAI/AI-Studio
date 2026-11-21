using AIStudio.Assistants.ModelComparison;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks that both chats of a task of a comparison series get the same setup.
/// </summary>
[TestFixture]
public sealed class ModelComparisonSeriesThreadsTests
{
    [Test]
    public void ANewChatHasTheSetupOfTheSeries()
    {
        var thread = ModelComparisonSeriesThreads.Create(Series(), "Weekly reports");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.Name, Is.EqualTo("Weekly reports"));
            Assert.That(thread.WorkspaceId, Is.EqualTo(Guid.Empty));
            Assert.That(thread.Blocks, Is.Empty);
            Assert.That(thread.SystemPrompt, Is.EqualTo(ModelComparisonSeriesThreads.SYSTEM_PROMPT));
            Assert.That(thread.SelectedProfile, Is.EqualTo("profile"));
            Assert.That(thread.SelectedChatTemplate, Is.EqualTo(ChatTemplate.NO_CHAT_TEMPLATE.Id));
            Assert.That(thread.SelectedToolIds, Is.EquivalentTo(new[] { "web_search" }));
            Assert.That(thread.DataSourceOptions.DisableDataSources, Is.False);
            Assert.That(thread.DataSourceOptions.PreselectedDataSourceIds, Is.EquivalentTo(new[] { "handbook" }));
            Assert.That(thread.DataSourceOptions.RetrievalMode, Is.EqualTo(DataSourceRetrievalMode.EVERY_MESSAGE));
        }
    }

    [Test]
    public void TheSystemPromptAsksTheModelNotToNameItself()
    {
        Assert.That(ModelComparisonSeriesThreads.SYSTEM_PROMPT, Does.StartWith(SystemPrompts.DEFAULT).And.Contains("Do not state your name"));
    }

    [Test]
    public void ARequestGetsTheSetupOfTheSeriesBack()
    {
        var series = Series();
        var thread = ModelComparisonSeriesThreads.Create(series, "chat");

        // What a request could have changed, e.g., a stored chat of an older version:
        thread.SystemPrompt = "something else";
        thread.SelectedProfile = "another profile";
        thread.DataSourceOptions = new DataSourceOptions { DisableDataSources = true, AutomaticDataSourceSelection = true };

        ModelComparisonSeriesThreads.PrepareForRequest(thread, series, AIStudio.Settings.Provider.NONE, ["web_search"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.SystemPrompt, Is.EqualTo(ModelComparisonSeriesThreads.SYSTEM_PROMPT));
            Assert.That(thread.SystemPromptOrigin, Is.Null);
            Assert.That(thread.SelectedProvider, Is.EqualTo(AIStudio.Settings.Provider.NONE.Id));
            Assert.That(thread.SelectedProfile, Is.EqualTo("profile"));
            Assert.That(thread.IncludeDateTime, Is.True);
            Assert.That(thread.RuntimeSelectedToolIds, Is.EquivalentTo(new[] { "web_search" }));
            Assert.That(thread.RuntimeToolsAreAssistantManaged, Is.True);
            Assert.That(thread.DataSourceOptions.DisableDataSources, Is.False);
            Assert.That(thread.DataSourceOptions.AutomaticDataSourceSelection, Is.False);
            Assert.That(thread.DataSourceOptions.AutomaticValidation, Is.False);
        }
    }

    [Test]
    public void ASeriesWithoutDataSourcesSearchesNone()
    {
        var thread = ModelComparisonSeriesThreads.Create(Series() with { DataSourceIds = [], RetrievalMode = null }, "chat");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(thread.DataSourceOptions.PreselectedDataSourceIds, Is.Empty);
        }
    }

    private static ModelComparisonSeries Series() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Weekly reports",
        ProfileId = "profile",
        ToolIds = ["web_search"],
        DataSourceIds = ["handbook"],
        RetrievalMode = DataSourceRetrievalMode.EVERY_MESSAGE,
    };
}