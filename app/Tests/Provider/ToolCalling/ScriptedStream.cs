using System.Runtime.CompilerServices;

using AIStudio.Provider;
using AIStudio.Tools.ToolCallingSystem.Harness;

namespace AIStudio.Tests.Provider.ToolCalling;

/// <summary>
/// Plays back the stream of a provider for the adapter tests, the way the shared stream reader hands it out.
/// </summary>
internal static class ScriptedStream
{
    /// <summary>
    /// Ends a stream the way a stop, a timeout, or a broken connection does: the lines before it go out, but the stream never
    /// reaches its end.
    /// </summary>
    public const string CUT_OFF = "<cut off>";

    /// <summary>
    /// Hands out the given payloads as data lines, and marks the end of the stream once all of them went out.
    /// </summary>
    /// <param name="data">The payloads, one per line; <see cref="CUT_OFF"/> cuts the stream off right there.</param>
    /// <param name="streamEnd">Marked when the stream reaches its end.</param>
    /// <param name="token">The cancellation token.</param>
    public static async IAsyncEnumerable<ServerSentEvent> Lines(string[] data, ServerSentEventStreamEnd streamEnd, [EnumeratorCancellation] CancellationToken token = default)
    {
        foreach (var line in data)
        {
            token.ThrowIfCancellationRequested();
            if (line is CUT_OFF)
                yield break;

            yield return new ServerSentEvent($"data: {line}", line);
        }

        streamEnd.MarkReached();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Runs the next round of the given adapter and tells whether it ended with an outcome.
    /// </summary>
    public static async Task<bool> RoundCompletesAsync(IToolCallingProviderAdapter adapter)
    {
        var completes = false;
        await foreach (var streamEvent in adapter.ExecuteRoundAsync(null, true))
            completes |= streamEvent.Kind is ToolCallingStreamEventKind.ROUND_COMPLETED;

        return completes;
    }
}