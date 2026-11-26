using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the server decides that the runtime is gone after a call to it failed.
/// </summary>
/// <remarks>
/// The probes answer from a script here, without waiting between them. The rounds have three probes, so
/// two unanswered probes in a row are not enough for a shutdown.
/// </remarks>
[TestFixture]
public sealed class RuntimeOutageCheckTests
{
    private static readonly RuntimeProbeSchedule THREE_PROBES = new(3, TimeSpan.Zero);

    [Test]
    public async Task AnAnswerEndsTheRoundWithoutShuttingDown()
    {
        var runtime = new ScriptedRuntime([false, true]);
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(runtime.IsAliveAsync, () => Interlocked.Increment(ref shutDowns), THREE_PROBES, CancellationToken.None, NullLogger.Instance);

        await check.Report("Tauri event stream error");

        Assert.That(shutDowns, Is.Zero, "The server shut down although the runtime answered.");
        Assert.That(runtime.Probes, Is.EqualTo(2), "The round went on after the runtime answered.");
    }

    [Test]
    public async Task NoAnswerShutsTheServerDownOnce()
    {
        var runtime = new ScriptedRuntime([false, false, false, false, false, false]);
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(runtime.IsAliveAsync, () => Interlocked.Increment(ref shutDowns), THREE_PROBES, CancellationToken.None, NullLogger.Instance);

        await check.Report("Tauri event stream error");
        await check.Report("EnterpriseEnvConfigs failed");

        Assert.That(shutDowns, Is.EqualTo(1));
        Assert.That(runtime.Probes, Is.EqualTo(3), "A report after the shutdown started another round.");
    }

    [Test]
    public async Task ReportsDuringARoundJoinIt()
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var probes = 0;
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(_ =>
        {
            Interlocked.Increment(ref probes);
            return answer.Task;
        }, () => Interlocked.Increment(ref shutDowns), THREE_PROBES, CancellationToken.None, NullLogger.Instance);

        var rounds = Enumerable.Range(0, 20).Select(_ => check.Report("Tauri event stream error")).ToList();
        answer.SetResult(true);
        await Task.WhenAll(rounds);

        Assert.That(probes, Is.EqualTo(1), "Every report started a round of its own.");
        Assert.That(rounds.Distinct().Count(), Is.EqualTo(1), "The reports did not join the running round.");
        Assert.That(shutDowns, Is.Zero);
    }

    /// <summary>
    /// Each round needs two of its three probes. Counted over the lifetime of the server, as before,
    /// these failed calls would have added up to a shutdown while the runtime kept running.
    /// </summary>
    [Test]
    public async Task FailedCallsDoNotAddUpAcrossRounds()
    {
        var runtime = new ScriptedRuntime([false, true, false, true, false, true]);
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(runtime.IsAliveAsync, () => Interlocked.Increment(ref shutDowns), THREE_PROBES, CancellationToken.None, NullLogger.Instance);

        for (var round = 0; round < 3; round++)
            await check.Report("Tauri event stream error");

        Assert.That(shutDowns, Is.Zero);
        Assert.That(runtime.Probes, Is.EqualTo(6));
    }

    [Test]
    public async Task AProbeWhichFailsCountsAsNoAnswer()
    {
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(_ => throw new HttpRequestException("Connection refused"), () => Interlocked.Increment(ref shutDowns), THREE_PROBES, CancellationToken.None, NullLogger.Instance);

        await check.Report("Tauri event stream error");

        Assert.That(shutDowns, Is.EqualTo(1));
    }

    [Test]
    public async Task StoppingTheServerEndsTheRoundQuietly()
    {
        using var stop = new CancellationTokenSource();
        var shutDowns = 0;
        var check = new RuntimeOutageCheck(_ =>
        {
            // The round awaits the check before the test leaves the scope of the source:
            // ReSharper disable once AccessToDisposedClosure
            stop.Cancel();
            return Task.FromResult(false);
        }, () => Interlocked.Increment(ref shutDowns), new RuntimeProbeSchedule(3, TimeSpan.FromHours(1)), stop.Token, NullLogger.Instance);

        var round = check.Report("Tauri event stream error");
        await round;

        Assert.That(round.IsCompletedSuccessfully, Is.True, "The round failed instead of ending quietly.");
        Assert.That(shutDowns, Is.Zero, "The server shut down although it was stopping anyway.");
    }

    /// <summary>
    /// Answers the probes from a script, one answer per probe.
    /// </summary>
    /// <param name="answers">Whether the runtime answers the first, second, and later probes.</param>
    private sealed class ScriptedRuntime(bool[] answers)
    {
        private int probes;

        /// <summary>
        /// How many probes asked the runtime so far.
        /// </summary>
        public int Probes => Volatile.Read(ref this.probes);

        public Task<bool> IsAliveAsync(CancellationToken token)
        {
            var probe = Interlocked.Increment(ref this.probes);
            return Task.FromResult(answers[probe - 1]);
        }
    }
}