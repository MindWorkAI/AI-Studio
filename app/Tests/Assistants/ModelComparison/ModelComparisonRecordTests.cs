using AIStudio.Assistants.ModelComparison;
using AIStudio.Provider;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks what the Model Comparison assistant keeps of a batch for the statistics.
/// </summary>
[TestFixture]
public sealed class ModelComparisonRecordTests
{
    private static readonly ModelComparisonModelIdentity FIRST = new() { LLMProvider = LLMProviders.OPEN_AI, ModelId = "first-model", Label = "First" };
    private static readonly ModelComparisonModelIdentity SECOND = new() { LLMProvider = LLMProviders.MISTRAL, ModelId = "second-model", Label = "Second" };

    [Test]
    public void TheRunsAreKeptInTheTermsOfTheModels()
    {
        var batch = new ModelComparisonBatch(2);

        // Column B is the first model here, and the judge prefers it too:
        batch.Add(Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, ModelComparisonVote.COLUMN_B));
        batch.Vote(ModelComparisonVote.COLUMN_B);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, judgePreferred: null));
        batch.SkipThisRun();

        var record = ModelComparisonRecord.Of(batch, DateTimeOffset.UtcNow, "Summarize it.", FIRST, SECOND, judge: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Id, Is.EqualTo(batch.Id), "Storing the same batch again replaces its file.");
            Assert.That(record.Runs, Has.Count.EqualTo(2));
            Assert.That(record.Runs[0].Vote, Is.EqualTo(ModelComparisonModelChoice.FIRST));
            Assert.That(record.Runs[0].JudgeChoice, Is.EqualTo(ModelComparisonModelChoice.FIRST));
            Assert.That(record.Runs[1].Vote, Is.Null);
            Assert.That(record.Runs[1].JudgeChoice, Is.Null);
            Assert.That(record.HoldsAnyChoice, Is.True);
        }
    }

    [Test]
    public void TheTimesAreKeptButNotTheAnswers()
    {
        var batch = new ModelComparisonBatch(1);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, judgePreferred: null));

        var record = ModelComparisonRecord.Of(batch, DateTimeOffset.UtcNow, "Summarize it.", FIRST, SECOND, judge: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Runs[0].First.Answered, Is.True);
            Assert.That(record.Runs[0].First.FirstTokenTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(record.Runs[0].First.TotalTime, Is.EqualTo(TimeSpan.FromSeconds(4)));
            Assert.That(record.Runs[0].Second.Answered, Is.False);
            Assert.That(record.Runs[0].Second.TotalTime, Is.Null);
        }
    }

    [Test]
    public void ABatchWithoutAnyChoiceHoldsNothingToCount()
    {
        var batch = new ModelComparisonBatch(1);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, judgePreferred: null));
        batch.SkipThisRun();

        var record = ModelComparisonRecord.Of(batch, DateTimeOffset.UtcNow, "Summarize it.", FIRST, SECOND, judge: null);

        Assert.That(record.HoldsAnyChoice, Is.False);
    }

    [Test]
    public void TheTimeIsKeptInUtc()
    {
        var batch = new ModelComparisonBatch(1);
        var finished = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(1));

        var record = ModelComparisonRecord.Of(batch, finished, "Summarize it.", FIRST, SECOND, judge: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.FinishedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
            Assert.That(record.FinishedAtUtc, Is.EqualTo(finished));
        }
    }

    [Test]
    public void OnlyASelfHostedModelHasAHost()
    {
        var cloud = new AIStudio.Settings.Provider(1, Guid.NewGuid().ToString(), "Cloud", LLMProviders.OPEN_AI, new("gpt-model", null));
        var selfHosted = new AIStudio.Settings.Provider(2, Guid.NewGuid().ToString(), "Server", LLMProviders.SELF_HOSTED, new("llama", null), IsSelfHosted: true, Hostname: "http://gpu-server:8080");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelComparisonModelIdentity.Of(cloud).Hostname, Is.Empty);
            Assert.That(ModelComparisonModelIdentity.Of(selfHosted).Hostname, Is.EqualTo("http://gpu-server:8080"));
            Assert.That(ModelComparisonModelIdentity.Of(selfHosted).ModelId, Is.EqualTo("llama"));
        }
    }

    [Test]
    public void TheSameModelIsRecognizedWhateverItsLabel()
    {
        var renamed = FIRST with { Label = "Renamed" };
        var otherModel = FIRST with { ModelId = "another-model" };
        var otherHost = FIRST with { Hostname = "http://elsewhere" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(FIRST.IsSameModelAs(renamed), Is.True);
            Assert.That(FIRST.IsSameModelAs(otherModel), Is.False);
            Assert.That(FIRST.IsSameModelAs(otherHost), Is.False);
            Assert.That(FIRST.IsSameModelAs(SECOND), Is.False);
        }
    }

    private static ModelComparisonBatchEntry Entry(ModelComparisonPresentationOrder order, ModelComparisonVote? judgePreferred) => new()
    {
        PresentationOrder = order,
        Result = new ModelComparisonRunResult
        {
            First = new ModelComparisonAnswer { Label = "First", Text = "An answer.", Completed = true, FirstTokenTime = TimeSpan.FromSeconds(1), TotalTime = TimeSpan.FromSeconds(4) },
            Second = judgePreferred is null
                ? new ModelComparisonAnswer { Label = "Second", Text = string.Empty, Completed = false }
                : new ModelComparisonAnswer { Label = "Second", Text = "Another answer.", Completed = true, FirstTokenTime = TimeSpan.FromSeconds(2), TotalTime = TimeSpan.FromSeconds(3) },
            Judge = judgePreferred is { } preferred ? new ModelComparisonJudgeVerdict { Label = "Judge", Completed = true, Preferred = preferred } : null,
        },
    };
}