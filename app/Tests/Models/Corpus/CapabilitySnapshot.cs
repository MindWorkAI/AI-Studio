using System.Runtime.CompilerServices;
using System.Text;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// Renders the corpus and its capabilities as one text, and says where that text is kept.
/// </summary>
/// <remarks>
/// The snapshot is compared as text rather than parsed back into entries. A model ID may be
/// anything a provider chooses to answer with, empty strings and separator characters included, and
/// a parser would have to be right about all of it to be worth anything. Comparing the rendered text
/// cannot be wrong about a name, and a diff of it reads the same in the test output as in the IDE.
/// </remarks>
public static class CapabilitySnapshot
{
    /// <summary>
    /// What a model with no capabilities at all is written as.
    /// </summary>
    /// <remarks>
    /// An empty column would be an invisible statement. Providers do answer with nothing: an empty
    /// model ID and the "no provider" entry both do, and both are in the corpus.
    /// </remarks>
    private const string NOTHING = "(nothing)";

    private const string HEADER =
        """
        # What the rules answer, for every model of the corpus.
        #
        # Generated. Do not edit by hand: run the SnapshotWriter test to write it anew, then read
        # the diff. Every line of it is a statement about a model which somebody has to agree with.
        #
        # Columns are provider, model ID as the provider reports it, the capabilities sorted by
        # name, and what the model is made for. The ID stands here unchanged, so a line may well
        # carry leading or trailing spaces.
        #
        # Models the audit found a wrong answer for are not in here. They live in
        # ExpectedChanges.cs, together with the answer they have to arrive at.
        #

        """;

    /// <summary>
    /// The directory this source file lives in, filled in by the compiler.
    /// </summary>
    /// <remarks>
    /// The snapshot is read from the source tree, not from the build output. It is a file somebody
    /// reviews and commits, so the test has to fail against the file in the working copy rather
    /// than against a stale copy next to the assembly.
    /// </remarks>
    private static readonly string DIRECTORY = ResolveDirectory();

    /// <summary>
    /// Where the snapshot is kept.
    /// </summary>
    public static readonly string FILE_PATH = Path.Combine(DIRECTORY, "CapabilitySnapshot.txt");

    /// <summary>
    /// Where a mismatching snapshot is written for comparison in the IDE.
    /// </summary>
    public static readonly string ACTUAL_FILE_PATH = Path.Combine(DIRECTORY, "CapabilitySnapshot.actual.txt");

    /// <summary>
    /// Renders the given entries and the capabilities the current rules answer with.
    /// </summary>
    /// <remarks>
    /// The text ends with the last model rather than with a line break, which is how this repository
    /// keeps its files. A generator disagreeing with that by one byte makes the test fail the next
    /// time an editor tidies the file up, and the failure says that nothing changed -- which is both
    /// true and useless.
    /// </remarks>
    /// <param name="entries">The entries to render.</param>
    /// <returns>The snapshot text, without a trailing newline and without carriage returns.</returns>
    public static string Render(IEnumerable<CorpusEntry> entries)
    {
        var lines = entries
            .OrderBy(entry => entry.Provider.ToString(), StringComparer.Ordinal)
            .ThenBy(entry => entry.ModelId, StringComparer.Ordinal)
            .Select(Line);

        return new StringBuilder(HEADER).AppendJoin('\n', lines).ToString();
    }

    /// <summary>
    /// Writes one corpus entry as a snapshot line.
    /// </summary>
    /// <remarks>
    /// The kind stands next to the capabilities rather than among them: the two answer different
    /// questions, and a model changing from a chat model into an embedding one is a different kind
    /// of news than a model gaining image input.
    /// </remarks>
    /// <param name="entry">The entry to write.</param>
    /// <returns>The line.</returns>
    private static string Line(CorpusEntry entry)
    {
        var profile = entry.Provider.GetModelProfile(new Model(entry.ModelId, null));
        return $"{entry.Provider} | {entry.ModelId} | {Describe(RebuiltRules.AsCapabilities(profile))} | {profile.Kind}";
    }

    /// <summary>
    /// Writes capabilities the way a snapshot line does.
    /// </summary>
    /// <remarks>
    /// Sorted by name, and duplicates are kept rather than folded away: a capability appearing twice
    /// is something to see, not something to hide.
    /// </remarks>
    /// <param name="capabilities">The capabilities to write.</param>
    /// <returns>The capability names, or a marker when there are none.</returns>
    public static string Describe(IEnumerable<Capability> capabilities)
    {
        var names = capabilities.Select(capability => capability.ToString()).Order(StringComparer.Ordinal).ToList();
        return names.Count is 0 ? NOTHING : string.Join(", ", names);
    }

    /// <summary>
    /// Reads the snapshot as it stands in the source tree.
    /// </summary>
    /// <returns>The snapshot text with its line endings normalized, or null when there is none yet.</returns>
    public static string? Read() => File.Exists(FILE_PATH) ? File.ReadAllText(FILE_PATH).Replace("\r\n", "\n") : null;

    private static string ResolveDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetDirectoryName(sourceFilePath)!;
}