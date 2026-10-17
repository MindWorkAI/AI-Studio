using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using AIStudio.Assistants.ModelComparison;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how a comparison run treats two models: that they are asked the same, at the same time,
/// and that whatever one of them does cannot spoil the result of the other.
/// </summary>
/// <remarks>
/// The models are made up here. What is under test is the run, not a provider.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonRunnerTests
{
    private static readonly ModelComparisonRequest REQUEST = new("The document.", "Summarize it.");

    private static readonly TimeSpan PATIENCE = TimeSpan.FromSeconds(5);

    [Test]
    public async Task BothModelsReceiveTheSamePrompt()
    {
        // Not REQUEST.ToPrompt() itself: the run adds a cache-busting request ID neither call here supplies:
        var prompts = new ConcurrentBag<string>();

        IAsyncEnumerable<string> Ask(string prompt, CancellationToken _)
        {
            prompts.Add(prompt);
            return Chunks("An answer.");
        }

        await CreateRunner().RunAsync(CreateParticipant("1", Ask), CreateParticipant("2", Ask), REQUEST, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(prompts, Has.Count.EqualTo(2));
            Assert.That(prompts.Distinct().Count(), Is.EqualTo(1), "Both models must receive the exact same prompt as each other.");
        });
    }

    [Test]
    public async Task RepeatedRunsGetDifferentPromptsSoACachingGatewayCannotAnswerFromACache()
    {
        var prompts = new ConcurrentBag<string>();

        IAsyncEnumerable<string> Ask(string prompt, CancellationToken _)
        {
            prompts.Add(prompt);
            return Chunks("An answer.");
        }

        var runner = CreateRunner();
        await runner.RunAsync(CreateParticipant("1", Ask), CreateParticipant("2", Ask), REQUEST, CancellationToken.None);
        await runner.RunAsync(CreateParticipant("1", Ask), CreateParticipant("2", Ask), REQUEST, CancellationToken.None);

        // Both models of the same run still share one prompt, the same as in BothModelsReceiveTheSamePrompt;
        // it is the two runs that must differ from each other, so a caching gateway cannot answer a
        // repeat run from what it already has cached for the first one:
        Assert.That(prompts.Distinct().Count(), Is.EqualTo(2), "Each run must see a prompt of its own, distinct from the other run's.");
    }

    [Test]
    public async Task TheModelsAreAskedAtTheSameTime()
    {
        var started = 0;
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        //
        // Neither model answers before the other one has been asked. Asked one after the other, the
        // first would wait in vain, run out of patience and fail, and so would the assertion below.
        //
        async IAsyncEnumerable<string> Ask(string _, [EnumeratorCancellation] CancellationToken token)
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.SetResult();

            await bothStarted.Task.WaitAsync(PATIENCE, token);
            yield return "An answer.";
        }

        var result = await CreateRunner().RunAsync(CreateParticipant("1", Ask), CreateParticipant("2", Ask), REQUEST, CancellationToken.None);

        Assert.That(result.BothCompleted, Is.True);
    }

    [Test]
    public async Task EachAnswerStaysWithTheModelWhichGaveIt()
    {
        var secondIsDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The first model finishes last, so a result assembled in the order of arrival would swap them:
        async IAsyncEnumerable<string> AskFirst(string _, [EnumeratorCancellation] CancellationToken token)
        {
            await secondIsDone.Task.WaitAsync(PATIENCE, token);
            yield return "from the first";
        }

        async IAsyncEnumerable<string> AskSecond(string _, [EnumeratorCancellation] CancellationToken __)
        {
            yield return "from the second";
            secondIsDone.TrySetResult();
            await Task.CompletedTask;
        }

        var result = await CreateRunner().RunAsync(CreateParticipant("1", AskFirst), CreateParticipant("2", AskSecond), REQUEST, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Text, Is.EqualTo("from the first"));
            Assert.That(result.First.Label, Is.EqualTo("Model 1"));
            Assert.That(result.Second.Text, Is.EqualTo("from the second"));
            Assert.That(result.Second.Label, Is.EqualTo("Model 2"));
        });
    }

    [Test]
    public async Task AnAnswerIsTheChunksJoinedWithoutTheThinking()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("<think>Let me think.", "</think>", "Hello", " world  ")),
            CreateParticipant("2", (_, _) => Chunks("Hi")),
            REQUEST,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Text, Is.EqualTo("Hello world"));
            Assert.That(result.First.Completed, Is.True);
        });
    }

    [Test]
    public async Task TheTimesAreMeasuredOnTheClockOfTheRun()
    {
        var clock = new ManualTimeProvider();

        async IAsyncEnumerable<string> AskFirst(string _, [EnumeratorCancellation] CancellationToken __)
        {
            clock.Advance(TimeSpan.FromMilliseconds(200));
            yield return "Hi";

            clock.Advance(TimeSpan.FromMilliseconds(300));
            yield return " there";
            await Task.CompletedTask;
        }

        // The second model does not touch the clock, so the times of the first one are the only ones which move:
        var result = await CreateRunner(clock).RunAsync(
            CreateParticipant("1", AskFirst),
            CreateParticipant("2", (_, _) => Failing()),
            REQUEST,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.FirstTokenTime, Is.EqualTo(TimeSpan.FromMilliseconds(200)));
            Assert.That(result.First.TotalTime, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
        });
    }

    [Test]
    public async Task AModelWhichFailsDoesNotTakeTheOtherOneDown()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Failing()),
            REQUEST,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Completed, Is.True);
            Assert.That(result.First.Text, Is.EqualTo("An answer."));
            Assert.That(result.Second.Completed, Is.False);
            Assert.That(result.Second.TotalTime, Is.Null, "A failed request has no total time.");
            Assert.That(result.BothCompleted, Is.False);
        });
    }

    [Test]
    public async Task AnAnswerWhichSaysNothingCountsAsAFailure()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks()),
            CreateParticipant("2", (_, _) => Chunks("   ", "\n")),
            REQUEST,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Completed, Is.False);
            Assert.That(result.Second.Completed, Is.False);
            Assert.That(result.BothCompleted, Is.False);
        });
    }

    [Test]
    public async Task AnAnswerWhichEndsInsideTheThinkingCountsAsAFailure()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("<think>Still thinking, and out of room")),
            CreateParticipant("2", (_, _) => Chunks("An answer.")),
            REQUEST,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Completed, Is.False);
            Assert.That(result.First.Text, Is.Empty);
            Assert.That(result.Second.Completed, Is.True);
        });
    }

    [Test]
    public async Task ACanceledRunEndsBothModelsWithoutACompletedAnswer()
    {
        using var cancellation = new CancellationTokenSource();

        async IAsyncEnumerable<string> Ask(string _, [EnumeratorCancellation] CancellationToken token)
        {
            yield return "A first piece";
            await Task.Delay(Timeout.Infinite, token);
        }

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
        var result = await CreateRunner().RunAsync(CreateParticipant("1", Ask), CreateParticipant("2", Ask), REQUEST, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.First.Completed, Is.False);
            Assert.That(result.Second.Completed, Is.False);
            Assert.That(result.First.TotalTime, Is.Null);
            Assert.That(result.BothCompleted, Is.False);
        });
    }

    [Test]
    public async Task TheJudgeIsAskedAboutBothCompleteAnswers()
    {
        string? judgePrompt = null;

        IAsyncEnumerable<string> AskJudge(string prompt, CancellationToken _)
        {
            judgePrompt = prompt;
            return Chunks("""{"preferred": "A", "reasoning": "It is more complete."}""");
        }

        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("Answer from the first model.")),
            CreateParticipant("2", (_, _) => Chunks("Answer from the second model.")),
            REQUEST,
            CancellationToken.None,
            judge: CreateParticipant("judge", AskJudge),
            judgeInstructions: "Prefer the more complete answer.");

        Assert.Multiple(() =>
        {
            Assert.That(judgePrompt, Does.Contain("Answer from the first model."));
            Assert.That(judgePrompt, Does.Contain("Answer from the second model."));
            Assert.That(judgePrompt, Does.Contain("Prefer the more complete answer."));
            Assert.That(result.Judge, Is.Not.Null);
            Assert.That(result.Judge!.Completed, Is.True);
            Assert.That(result.Judge.Preferred, Is.EqualTo(ModelComparisonVote.COLUMN_A));
            Assert.That(result.Judge.Label, Is.EqualTo("Model judge"));
        });
    }

    [Test]
    public async Task TheJudgeIsToldTheAnswersInPresentationOrder()
    {
        string? judgePrompt = null;

        IAsyncEnumerable<string> AskJudge(string prompt, CancellationToken _)
        {
            judgePrompt = prompt;
            return Chunks("""{"preferred": "A", "reasoning": "n/a"}""");
        }

        // The second model is drawn first, so it is what the judge is told is "Answer A":
        await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("Answer from the first model.")),
            CreateParticipant("2", (_, _) => Chunks("Answer from the second model.")),
            REQUEST,
            CancellationToken.None,
            ModelComparisonPresentationOrder.SECOND_MODEL_FIRST,
            CreateParticipant("judge", AskJudge));

        Assert.Multiple(() =>
        {
            Assert.That(judgePrompt, Does.Match(@"# Answer A\r?\nAnswer from the second model\."));
            Assert.That(judgePrompt, Does.Match(@"# Answer B\r?\nAnswer from the first model\."));
        });
    }

    [Test]
    public async Task TheJudgeIsNotAskedWhenAModelDidNotAnswer()
    {
        var judgeWasAsked = false;

        IAsyncEnumerable<string> AskJudge(string _, CancellationToken __)
        {
            judgeWasAsked = true;
            return Chunks("""{"preferred": "TIE", "reasoning": "n/a"}""");
        }

        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Failing()),
            REQUEST,
            CancellationToken.None,
            judge: CreateParticipant("judge", AskJudge));

        Assert.Multiple(() =>
        {
            Assert.That(judgeWasAsked, Is.False, "A judge would only ever see one complete answer, which is nothing to judge.");
            Assert.That(result.Judge, Is.Null);
        });
    }

    [Test]
    public async Task WithoutAJudgeTheResultHasNoVerdict()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            CancellationToken.None);

        Assert.That(result.Judge, Is.Null);
    }

    [Test]
    public async Task AJudgeWhichFailsLeavesAVerdictWhichIsNotCompleted()
    {
        var result = await CreateRunner().RunAsync(
            CreateParticipant("1", (_, _) => Chunks("An answer.")),
            CreateParticipant("2", (_, _) => Chunks("Another answer.")),
            REQUEST,
            CancellationToken.None,
            judge: CreateParticipant("judge", (_, _) => Failing()));

        Assert.Multiple(() =>
        {
            Assert.That(result.Judge, Is.Not.Null);
            Assert.That(result.Judge!.Completed, Is.False);
        });
    }

    private static ModelComparisonRunner CreateRunner(TimeProvider? clock = null) => new(NullLogger.Instance, clock);

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

    private static async IAsyncEnumerable<string> Failing()
    {
        await Task.Yield();
        throw new InvalidOperationException("The model is not reachable.");

        #pragma warning disable CS0162 // Unreachable code: the yield is what makes this an iterator
        yield break;
        #pragma warning restore CS0162
    }

    /// <summary>
    /// A clock which only moves when a test says so.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long ticks;

        // One timestamp is one tick of a TimeSpan, so the elapsed time of the runner is exactly what Advance added:
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan by) => Interlocked.Add(ref this.ticks, by.Ticks);

        public override long GetTimestamp() => Interlocked.Read(ref this.ticks);
    }
}