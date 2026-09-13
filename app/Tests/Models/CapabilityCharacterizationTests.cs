using System.Text;

using AIStudio.Tests.Models.Corpus;

namespace AIStudio.Tests.Models;

/// <summary>
/// Holds the current capability rules to their word, model by model.
/// </summary>
/// <remarks>
/// These tests state nothing about what is right. They state what the code answers today, so that
/// rebuilding the capability system cannot change an answer by accident: every difference shows up
/// here and has to be either a porting mistake or a decision somebody wrote down.
///
/// When a diff appears, read it before touching anything. If every line of it is wanted, run the
/// snapshot writer and commit the new file together with the change that caused it.
/// </remarks>
[TestFixture]
public sealed class CapabilityCharacterizationTests
{
    /// <summary>
    /// How many differing lines the failure message shows before it stops.
    /// </summary>
    private const int LINES_SHOWN = 25;

    /// <summary>
    /// How many columns a snapshot line carries after the model ID.
    /// </summary>
    /// <remarks>
    /// The capabilities, the kind, the context window, the image limit, and the tokenizer. Adding a
    /// column to the snapshot means raising this, and forgetting to would split a line inside its
    /// last column instead of in front of it -- which makes every model look changed at once.
    /// </remarks>
    private const int TRAILING_COLUMNS = 5;

    [Test]
    public void TheCorpusStillGetsTheAnswersTheSnapshotRecorded()
    {
        var recorded = CapabilitySnapshot.Read();
        var current = CapabilitySnapshot.Render(ModelCorpus.ENTRIES);

        if (recorded is null)
        {
            File.WriteAllText(CapabilitySnapshot.FILE_PATH, current);
            Assert.Fail($"There was no snapshot yet, so one was written to {CapabilitySnapshot.FILE_PATH}. Read it line by line and commit it, then this test turns green.");
            return;
        }

        if (recorded == current)
        {
            //
            // A leftover file from an earlier failure would otherwise sit in the working tree and
            // get committed by somebody who did not notice it:
            //
            File.Delete(CapabilitySnapshot.ACTUAL_FILE_PATH);
            return;
        }

        File.WriteAllText(CapabilitySnapshot.ACTUAL_FILE_PATH, current);
        Assert.Fail($"The capabilities of {DescribeDifference(recorded, current)}{Environment.NewLine}{Environment.NewLine}The full result was written to {CapabilitySnapshot.ACTUAL_FILE_PATH}.");
    }

    /// <summary>
    /// Describes how two snapshots differ, in the words of the lines that differ.
    /// </summary>
    /// <param name="recorded">The snapshot as it was recorded.</param>
    /// <param name="current">The snapshot as the code answers now.</param>
    /// <returns>A description naming the changed, added, and removed lines.</returns>
    private static string DescribeDifference(string recorded, string current)
    {
        var recordedLines = ModelLinesOf(recorded);
        var currentLines = ModelLinesOf(current);

        var changed = recordedLines.Keys.Intersect(currentLines.Keys).Where(model => recordedLines[model] != currentLines[model]).ToList();
        var added = currentLines.Keys.Except(recordedLines.Keys).ToList();
        var removed = recordedLines.Keys.Except(currentLines.Keys).ToList();

        var message = new StringBuilder($"{changed.Count} model(s) changed, {added.Count} came into the corpus, {removed.Count} left it:").Append(Environment.NewLine);
        foreach (var model in changed.Take(LINES_SHOWN))
        {
            message.Append(Environment.NewLine).Append("  ").Append(model);
            message.Append(Environment.NewLine).Append("    was: ").Append(recordedLines[model]);
            message.Append(Environment.NewLine).Append("    now: ").Append(currentLines[model]);
        }

        foreach (var model in added.Take(LINES_SHOWN))
            message.Append(Environment.NewLine).Append("  + ").Append(model).Append(": ").Append(currentLines[model]);

        foreach (var model in removed.Take(LINES_SHOWN))
            message.Append(Environment.NewLine).Append("  - ").Append(model).Append(": ").Append(recordedLines[model]);

        return message.ToString();
    }

    /// <summary>
    /// Splits a snapshot into what each line says about which model.
    /// </summary>
    /// <remarks>
    /// The provider and the model ID make up everything before the trailing columns, and those are
    /// the one place a split is safe: a model ID may contain anything, while the capability list,
    /// the kind and the context window may not.
    /// </remarks>
    /// <param name="snapshot">The snapshot text.</param>
    /// <returns>What every line says about a model, keyed by provider and model.</returns>
    private static Dictionary<string, string> ModelLinesOf(string snapshot)
    {
        var lines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in snapshot.Split('\n'))
        {
            if (line.Length is 0 || line.StartsWith('#'))
                continue;

            var separatorIndex = line.Length;
            for (var column = 0; column < TRAILING_COLUMNS; column++)
            {
                separatorIndex = line.LastIndexOf(" | ", separatorIndex - 1, StringComparison.Ordinal);
                if (separatorIndex is -1)
                    break;
            }

            if (separatorIndex is -1)
                continue;

            lines[line[..separatorIndex]] = line[(separatorIndex + 3)..];
        }

        return lines;
    }
}