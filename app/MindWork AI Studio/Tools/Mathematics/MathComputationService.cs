using System.Globalization;

using AIStudio.Chat;

using AngouriMath;
using AngouriMath.Core.Exceptions;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Runs the computations of the Mathematics tools, one at a time and within a time budget.
/// </summary>
/// <remarks>
/// AngouriMath parses and simplifies recursively. About 500 nested parentheses overflow the stack of
/// a thread pool thread, which ends the whole process, so every computation runs on a thread of its
/// own with a stack of 64 MB. That holds the deepest nesting an expression within
/// MathLimits.MAX_EXPRESSION_LENGTH can have.<br/><br/>
/// Only one computation runs at a time, because AI Studio also runs on a Raspberry Pi. AngouriMath
/// stops at the cancellation token of its thread wherever it checks it, and that token ends with the
/// budget or with the request. Some computations never check it, such as an exact power with
/// millions of digits; MathGuards refuses those which can be told beforehand. One which still runs
/// past its budget keeps the slot until it ends: the call gives up and says so, and the next
/// computations wait for the slot instead of taking another core.<br/><br/>
/// The settings of AngouriMath belong to the flow which sets them. Each computation reads
/// expressions explicitly and solves without Newton's method, see AngouriMathBehaviorTests.
/// </remarks>
/// <param name="logger">The logger.</param>
public sealed class MathComputationService(ILogger<MathComputationService> logger)
{
    private const int STACK_SIZE_BYTES = 64 * 1024 * 1024;

    private const int MAX_MESSAGE_LENGTH = 400;

    /// <summary>
    /// How long a computation may take, and how long it waits for the slot before that.
    /// </summary>
    public static readonly TimeSpan DEFAULT_TIME_BUDGET = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a call waits beyond the budget for a computation to stop at its cancellation token.
    /// </summary>
    private static readonly TimeSpan STOP_GRACE = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim slot = new(1, 1);

    /// <summary>
    /// How long a computation may take. Tests shorten it.
    /// </summary>
    internal TimeSpan TimeBudget { get; init; } = DEFAULT_TIME_BUDGET;

    /// <summary>
    /// Runs a computation on a thread of its own, once the slot is free.
    /// </summary>
    /// <remarks>
    /// Parse the expressions inside the computation, too: the parser needs the stack of its thread.
    /// An exception of AngouriMath comes back as a MathComputationException with its message.
    /// </remarks>
    /// <param name="computation">The computation, which gets the token of its budget.</param>
    /// <param name="token">The cancellation token of the request.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The result of the computation.</returns>
    /// <exception cref="MathComputationException">The computation was refused, failed, or did not finish within its budget.</exception>
    public async Task<T> ComputeAsync<T>(Func<CancellationToken, T> computation, CancellationToken token = default)
    {
        if (!await this.slot.WaitAsync(this.TimeBudget, token))
            throw new MathComputationException($"Another computation is still running, so this one could not start within {this.BudgetInSeconds} seconds.");

        // The thread disposes the budget and frees the slot once its computation ended. Should the thread not
        // even start, the slot is freed below, and the budget is left to the garbage collector:
        var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => this.Run(computation, budget, completion, token), STACK_SIZE_BYTES)
        {
            IsBackground = true,
            Name = "Mathematics",
        };

        try
        {
            budget.CancelAfter(this.TimeBudget);
            thread.Start();
        }
        catch
        {
            this.slot.Release();
            throw;
        }

        try
        {
            return await completion.Task.WaitAsync(this.TimeBudget + STOP_GRACE, token);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("A computation of the Mathematics tools ran past its time budget of {Seconds} seconds without stopping. It keeps the computing slot until it ends.", this.BudgetInSeconds);
            throw new MathComputationException($"The computation did not finish within {this.BudgetInSeconds} seconds and could not be stopped. It keeps running until it ends, and other computations wait for it meanwhile.");
        }
    }

    private string BudgetInSeconds => this.TimeBudget.TotalSeconds.ToString(CultureInfo.InvariantCulture);

    private void Run<T>(Func<CancellationToken, T> computation, CancellationTokenSource budget, TaskCompletionSource<T> completion, CancellationToken token)
    {
        try
        {
            MathS.Multithreading.SetLocalCancellationToken(budget.Token);
            using var explicitParsing = MathS.Settings.ExplicitParsingOnly.Set(true);
            using var withoutNewton = MathS.Settings.AllowNewton.Set(false);
            completion.TrySetResult(computation(budget.Token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            completion.TrySetCanceled(token);
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            completion.TrySetException(new MathComputationException($"The computation did not finish within {this.BudgetInSeconds} seconds."));
        }
        catch (AngouriMathBaseException exception)
        {
            logger.LogInformation("AngouriMath could not compute an expression: {Message}", exception.Message);
            completion.TrySetException(new MathComputationException($"AngouriMath could not compute this: {exception.Message.Shorten(MAX_MESSAGE_LENGTH)}"));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            budget.Dispose();
            this.slot.Release();
        }
    }
}