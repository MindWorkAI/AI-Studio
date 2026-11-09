using AIStudio.Tools.Mathematics;

using AngouriMath;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Mathematics;

[TestFixture]
public sealed class MathComputationServiceTests
{
    private static readonly TimeSpan SHORT_BUDGET = TimeSpan.FromMilliseconds(200);

    private static MathComputationService CreateService(TimeSpan? budget = null) => new(NullLogger<MathComputationService>.Instance)
    {
        TimeBudget = budget ?? MathComputationService.DEFAULT_TIME_BUDGET,
    };

    /// <summary>
    /// A computation which checks its token, and nothing else.
    /// </summary>
    private static int SpinUntilCanceled(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
            Thread.SpinWait(100);

        token.ThrowIfCancellationRequested();
        return 0;
    }

    [Test]
    public async Task AComputationRunsWithTheSettingsOfTheTools()
    {
        var service = CreateService();
        var settings = await service.ComputeAsync(_ => new[]
        {
            Thread.CurrentThread.Name,
            MathS.Settings.ExplicitParsingOnly.Value.ToString(),
            MathS.Settings.AllowNewton.Value.ToString(),
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Is.EqualTo(new[] { "Mathematics", "True", "False" }));
            Assert.That(MathS.Settings.ExplicitParsingOnly.Value, Is.False, "The settings stay with the computation.");
        }
    }

    [Test]
    public async Task TheLongestExpressionsDoNotOverflowTheStack()
    {
        // Each of these holds as deep a tree as 2,000 characters allow; on a thread pool thread,
        // the first one would end the test run:
        var service = CreateService();
        var parentheses = new string('(', 999) + "1" + new string(')', 999);
        var powerTower = string.Join("^", Enumerable.Repeat("x", 1000));
        var sineChain = string.Concat(Enumerable.Repeat("sin(", 399)) + "1" + new string(')', 399);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await service.ComputeAsync(_ => MathExpressionReader.Read(parentheses, "expression").Evaled), Is.EqualTo(Entity.Number.Integer.Create(1)));
            Assert.That(await service.ComputeAsync(_ => MathExpressionReader.Read(powerTower, "expression").Simplify().ToString().Length), Is.GreaterThan(1000));
            Assert.That(await service.ComputeAsync(_ => MathExpressionReader.Read(sineChain, "expression").EvalNumerical().ToString()), Does.StartWith("0.08"));
        }
    }

    [Test]
    public async Task AComputationWhichChecksItsTokenStopsAtTheBudget()
    {
        var service = CreateService(SHORT_BUDGET);
        var exception = (await Assert.ThrowsAsync<MathComputationException>(() => service.ComputeAsync(SpinUntilCanceled)))!;

        Assert.That(exception.Message, Is.EqualTo("The computation did not finish within 0.2 seconds."));
    }

    [Test]
    public async Task AComputationWhichIgnoresItsTokenKeepsTheSlotUntilItEnds()
    {
        // Not disposed: the stuck computation may still be waiting for it when the test ends.
        var service = CreateService(SHORT_BUDGET);
        var release = new ManualResetEventSlim();
        var stuck = (await Assert.ThrowsAsync<MathComputationException>(() => service.ComputeAsync(_ =>
        {
            release.Wait(CancellationToken.None);
            return 1;
        })))!;

        var waiting = (await Assert.ThrowsAsync<MathComputationException>(() => service.ComputeAsync(_ => 2)))!;
        release.Set();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stuck.Message, Does.StartWith("The computation did not finish within 0.2 seconds and could not be stopped."));
            Assert.That(waiting.Message, Is.EqualTo("Another computation is still running, so this one could not start within 0.2 seconds."));
            Assert.That(await service.ComputeAsync(_ => 3), Is.EqualTo(3), "Once the stuck computation ends, the slot is free again.");
        }
    }

    [Test]
    public void TheRequestCancelsAComputation()
    {
        var service = CreateService();
        var request = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Assert.That(() => service.ComputeAsync(SpinUntilCanceled, request.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task AnExceptionOfAngouriMathBecomesAMessageAndFreesTheSlot()
    {
        var service = CreateService();
        var exception = (await Assert.ThrowsAsync<MathComputationException>(() => service.ComputeAsync(_ => MathExpressionReader.Read("x + 1", "expression").EvalNumerical())))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.StartWith("The tool could not compute this:`x + 1` evaluates to `x + 1`"));
            Assert.That(await service.ComputeAsync(_ => 4), Is.EqualTo(4));
        }
    }
}