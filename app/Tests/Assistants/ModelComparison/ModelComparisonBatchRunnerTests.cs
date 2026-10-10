using System.Runtime.CompilerServices;

using AIStudio.Assistants.ModelComparison;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how a batch makes several independent runs of the same comparison.
/// </summary>
/// <remarks>
/// What is under test is that every run is genuinely independent -- its own request to both models,
/// its own presentation order -- not the single run itself, which <see cref="ModelComparisonRunnerTests"/>
/// already covers.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonBatchRunnerTests
{
    private static readonly ModelComparisonRequest REQUEST = new("The document.", "Summarize it.");

    [Test]
    public async Task TheRequestedNumberOfRunsIsMade()
    {
        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            runCount: 5,
            CancellationToken.None);

        var entries = await Task.WhenAll(tasks);

        Assert.That(entries, Has.Length.EqualTo(5));
    }

    [Test]
    public void EveryRunStartsWithoutWaitingForAnyOfThemToFinish()
    {
        // Nothing here completes on its own; Start must still hand back its tasks right away. The
        // cancellation afterward is only cleanup, so the never-ending calls do not outlive the test:
        using var cancellation = new CancellationTokenSource();

        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", (_, token) => Never(token)),
            CreateParticipant("2", (_, token) => Never(token)),
            REQUEST,
            runCount: 3,
            cancellation.Token);

        try
        {
            Assert.That(tasks, Has.Count.EqualTo(3));
        }
        finally
        {
            cancellation.Cancel();
        }
    }

    [Test]
    public async Task NoMoreRunsThanTheLimitAskAModelAtOnce()
    {
        // Ten runs, twenty model calls between them; if every run started at once, every one of
        // those twenty calls would be concurrent. A short delay gives the batch runner room to queue
        // the rest behind the limit, which the peak below has to stay within -- two models per run:
        var concurrentCalls = 0;
        var peakConcurrentCalls = 0;
        var peakLock = new object();

        async IAsyncEnumerable<string> Ask(string _, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            var current = Interlocked.Increment(ref concurrentCalls);
            lock (peakLock)
                peakConcurrentCalls = Math.Max(peakConcurrentCalls, current);

            await Task.Delay(TimeSpan.FromMilliseconds(30), token);
            Interlocked.Decrement(ref concurrentCalls);
            yield return "An answer.";
        }

        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", Ask),
            CreateParticipant("2", Ask),
            REQUEST,
            runCount: 10,
            CancellationToken.None);

        await Task.WhenAll(tasks);

        var expectedPeak = ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS * 2;
        Assert.That(peakConcurrentCalls, Is.LessThanOrEqualTo(expectedPeak), $"At most {ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS} runs at a time, two model calls each.");
    }

    [Test]
    public async Task EachRunAsksBothModelsAfresh()
    {
        var firstAskedCount = 0;
        var secondAskedCount = 0;

        IAsyncEnumerable<string> AskFirst(string _, CancellationToken __)
        {
            Interlocked.Increment(ref firstAskedCount);
            return Chunks("An answer.");
        }

        IAsyncEnumerable<string> AskSecond(string _, CancellationToken __)
        {
            Interlocked.Increment(ref secondAskedCount);
            return Chunks("Another answer.");
        }

        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", AskFirst),
            CreateParticipant("2", AskSecond),
            REQUEST,
            runCount: 4,
            CancellationToken.None);

        await Task.WhenAll(tasks);

        Assert.Multiple(() =>
        {
            Assert.That(firstAskedCount, Is.EqualTo(4));
            Assert.That(secondAskedCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task EveryRunCompletesOnItsOwnEvenWhenOneFails()
    {
        // Every third run fails; the rest must not be dragged down with it:
        var callNumber = 0;

        IAsyncEnumerable<string> Flaky(string _, CancellationToken __)
        {
            var thisCall = Interlocked.Increment(ref callNumber);
            return thisCall % 3 == 0 ? Failing() : Chunks("An answer.");
        }

        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", Flaky),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            runCount: 6,
            CancellationToken.None);

        var entries = await Task.WhenAll(tasks);

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Length.EqualTo(6));
            Assert.That(entries.Count(entry => entry.Result.BothCompleted), Is.EqualTo(4));
            Assert.That(entries.Count(entry => !entry.Result.BothCompleted), Is.EqualTo(2));
        });
    }

    [Test]
    public void ARunCountBelowOneIsRefused()
    {
        var batchRunner = CreateBatchRunner();

        Assert.That(() => batchRunner.Start(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            runCount: 0,
            CancellationToken.None), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public async Task BothPresentationOrdersCanComeUp()
    {
        // With enough runs, drawing only ever one of the two orders would be a bug, not bad luck:
        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            runCount: 60,
            CancellationToken.None);

        var entries = await Task.WhenAll(tasks);
        var orders = entries.Select(entry => entry.PresentationOrder).Distinct().ToArray();

        Assert.That(orders, Is.EquivalentTo(Enum.GetValues<ModelComparisonPresentationOrder>()));
    }

    [Test]
    public async Task WithAJudgeEveryRunHasItsOwnVerdict()
    {
        var judgeAskedCount = 0;

        IAsyncEnumerable<string> AskJudge(string _, CancellationToken __)
        {
            Interlocked.Increment(ref judgeAskedCount);
            return Chunks("""{"preferred": "A", "reasoning": "n/a"}""");
        }

        var tasks = CreateBatchRunner().Start(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            runCount: 3,
            CancellationToken.None,
            judge: CreateParticipant("judge", AskJudge));

        var entries = await Task.WhenAll(tasks);

        Assert.Multiple(() =>
        {
            Assert.That(judgeAskedCount, Is.EqualTo(3));
            Assert.That(entries.All(entry => entry.Result.Judge is { Completed: true }), Is.True);
        });
    }

    private static ModelComparisonBatchRunner CreateBatchRunner() => new(new ModelComparisonRunner(NullLogger.Instance));

    private static ModelComparisonParticipant CreateParticipant(string id, Func<string, CancellationToken, IAsyncEnumerable<string>> ask) => new()
    {
        Label = $"Model {id}",
        Ask = ask,
    };

    private static async IAsyncEnumerable<string> Chunks(params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return chunk;
        }
    }

    private static async IAsyncEnumerable<string> Never([EnumeratorCancellation] CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
            // Expected once the test cleans up; there is nothing left to answer with.
        }

        #pragma warning disable CS0162 // Unreachable code: the yield is what makes this an iterator
        yield break;
        #pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<string> Failing()
    {
        await Task.Yield();
        throw new InvalidOperationException("The model is not reachable.");

        #pragma warning disable CS0162 // Unreachable code: the yield is what makes this an iterator
        yield break;
        #pragma warning restore CS0162
    }
}
