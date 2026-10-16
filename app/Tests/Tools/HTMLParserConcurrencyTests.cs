using System.Collections.Concurrent;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that converting several pages to Markdown at the same time keeps them apart.
/// </summary>
/// <remarks>
/// A web search reads up to four result pages in parallel, and every one of them is converted
/// through the same entry point. The converter doing that work tracks the ancestors of the node it
/// is at, and it does so without any synchronization, so sharing one converter between those
/// conversions let them write into each other's ancestor lists.<br/><br/>
/// That went wrong in two ways, and this test covers both. Loudly, as a torn list throwing an index
/// out of range — which is what showed up in the logs. And quietly, as a list indented by the depth
/// a different page happened to be at, which nothing reports and which only a comparison against a
/// known-good conversion catches.
/// </remarks>
[TestFixture]
public sealed class HTMLParserConcurrencyTests
{
    private const int THREAD_COUNT = 8;
    private const int CONVERSIONS_PER_THREAD = 40;

    [Test]
    public void ParallelConversionsDoNotInterfereWithEachOther()
    {
        var html = BuildPageHtml();

        // Converted alone, with nothing else running, this is what the page has to come back as:
        var expected = HTMLParser.ParseToMarkdown(html);

        var results = new ConcurrentBag<string>();
        var failures = new ConcurrentBag<Exception>();

        //
        // Real threads released by a barrier rather than a parallel loop: the conversions have to
        // overlap for this test to mean anything, and only starting them together makes that
        // certain.
        //
        // What a thread works with is handed over when it starts rather than captured. The barrier
        // is disposed at the end of this method, and while the joins below make sure no thread is
        // still at it by then, that is nothing one can see from inside a lambda.
        //
        using var startSignal = new Barrier(THREAD_COUNT);
        var threads = new List<Thread>(THREAD_COUNT);
        for (var threadIndex = 0; threadIndex < THREAD_COUNT; threadIndex++)
        {
            var thread = new Thread(ConvertRepeatedly);
            thread.Start(new ConversionRun(startSignal, html, results, failures));
            threads.Add(thread);
        }

        foreach (var thread in threads)
            thread.Join();

        var failureKinds = string.Join(", ", failures.Select(x => x.GetType().Name).Distinct(StringComparer.Ordinal));
        var deviatingCount = results.Count(x => !string.Equals(x, expected, StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(failures, Is.Empty, $"Converting in parallel threw {failures.Count} times ({failureKinds}). A conversion must not depend on what another thread is converting.");
            Assert.That(deviatingCount, Is.Zero, $"{deviatingCount} of {results.Count} conversions came back different from the same page converted on its own. Their indentation was counted from ancestors belonging to another conversion.");
        });
    }

    /// <summary>
    /// Converts the same page over and over, once every thread has arrived at the barrier.
    /// </summary>
    private static void ConvertRepeatedly(object? state)
    {
        var run = (ConversionRun)state!;
        run.StartSignal.SignalAndWait();

        for (var conversion = 0; conversion < CONVERSIONS_PER_THREAD; conversion++)
        {
            try
            {
                run.Results.Add(HTMLParser.ParseToMarkdown(run.Html));
            }
            catch (Exception exception)
            {
                run.Failures.Add(exception);
            }
        }
    }

    /// <summary>
    /// Builds a page out of the elements the reported stack traces named.
    /// </summary>
    /// <remarks>
    /// The nested lists are what makes this sharp: their indentation is computed from the ancestors
    /// the converter is tracking, so a conversion which picked up somebody else's ancestors comes
    /// back indented differently rather than failing outright. The block is repeated so that the
    /// conversions take long enough to actually overlap.
    /// </remarks>
    private static string BuildPageHtml()
    {
        const string BLOCK =
            """
            <div>
              <p>An introduction to the topic at hand.</p>
              <ol>
                <li>First item
                  <ul>
                    <li>Nested item
                      <ol>
                        <li>Deeply nested item</li>
                        <li>Another one
                          <ul><li>And one level deeper still</li></ul>
                        </li>
                      </ol>
                    </li>
                  </ul>
                </li>
                <li>Second item</li>
              </ol>
              <table>
                <tr><th>Column A</th><th>Column B</th></tr>
                <tr>
                  <td><div><p>A cell holding a paragraph.</p></div></td>
                  <td><ul><li>A cell holding a list</li><li>with two entries</li></ul></td>
                </tr>
              </table>
              <p>A closing paragraph with <strong>bold</strong> and <em>emphasized</em> text.</p>
            </div>
            """;

        return string.Concat(Enumerable.Repeat(BLOCK, 20));
    }

    /// <summary>
    /// Everything one thread of this test needs, so that it is passed rather than captured.
    /// </summary>
    private sealed record ConversionRun(Barrier StartSignal, string Html, ConcurrentBag<string> Results, ConcurrentBag<Exception> Failures);
}