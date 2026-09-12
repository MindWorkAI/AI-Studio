using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using AIStudio.Models;
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

    /// <summary>
    /// What a model nobody stated a context window for is written as.
    /// </summary>
    /// <remarks>
    /// Deliberately not a zero. A window nobody has looked up is a different statement from a
    /// window of no tokens, and reading the two as the same is the mistake this whole rebuild set
    /// out to stop making.
    /// </remarks>
    private const string NO_WINDOW = "(unknown)";

    /// <summary>
    /// What a model nobody stated an image limit for is written as.
    /// </summary>
    /// <remarks>
    /// The same reasoning as the window, and the same warning against reading it as a zero: a model
    /// whose vendor says nothing takes as many images as it takes, and the app treats it that way.
    /// </remarks>
    private const string NO_IMAGE_LIMIT = "(unknown)";

    /// <summary>
    /// What a model nobody named a tokenizer for is written as.
    /// </summary>
    /// <remarks>
    /// Which is what the app already does for all of them: it counts with the tokenizer it ships
    /// and says that the number is an estimate. Naming one changes nothing about the counting yet;
    /// it tells a person which file to look for, and for two vendors that there is none.
    /// </remarks>
    private const string NO_TOKENIZER = "(unknown)";

    private const string HEADER =
        """
        # What the rules answer, for every model of the corpus.
        #
        # Generated. Do not edit by hand: run the SnapshotWriter test to write it anew, then read
        # the diff. Every line of it is a statement about a model which somebody has to agree with.
        #
        # Columns are provider, model ID as the provider reports it, the capabilities sorted by
        # name, what the model is made for, its context window in tokens, how many images it
        # accepts, and which tokenizer it uses. The ID stands here unchanged, so a line may well
        # carry leading or trailing spaces.
        #
        # A window or an image limit written as "(unknown)" is one nobody has stated a source for.
        # That is a gap, not a claim: the app then shows a person how many tokens their conversation
        # uses without telling them what it may grow to, and it stops nobody from attaching a
        # hundred pictures to a model which may well take them.
        #
        # Every model of the corpus stands here, the ones the audit found a wrong answer for
        # included. While the old rules still stood those were kept out, so that a known-wrong
        # answer could not be frozen into this file. The old rules are gone and their answers are
        # corrected, so keeping them out only hid four of their columns: ExpectedChanges.cs states
        # what each of them must answer, but it states capabilities alone.
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
        return $"{entry.Provider} | {entry.ModelId} | {Describe(RebuiltRules.AsCapabilities(profile))} | {profile.Kind} | {Describe(profile.Context)} | {Describe(profile.Images)} | {Describe(profile.Tokenizer)}";
    }

    /// <summary>
    /// Writes a tokenizer reference the way a snapshot line does.
    /// </summary>
    /// <remarks>
    /// The kind travels with the name, because the name alone would be a riddle: "o200k_base" is
    /// not a repository somebody can open, and "/v1/messages/count_tokens" is not a file somebody
    /// can download. What sort of thing it is decides what a person can do with it.
    /// </remarks>
    /// <param name="tokenizer">The reference to write.</param>
    /// <returns>The reference, or a marker when nobody named one.</returns>
    public static string Describe(TokenizerRef tokenizer) => tokenizer.IsKnown ? $"{tokenizer.Kind} {tokenizer.Id}" : NO_TOKENIZER;

    /// <summary>
    /// Writes an image limit the way a snapshot line does.
    /// </summary>
    /// <remarks>
    /// Both numbers are named where both are known, because they answer different questions and a
    /// vendor may state either alone. Naming the one which happens to be smaller would turn two
    /// statements into one and lose which of them was actually read from a page.
    /// </remarks>
    /// <param name="limits">The limits to write.</param>
    /// <returns>The limits, or a marker when nobody stated any.</returns>
    public static string Describe(ImageLimits limits)
    {
        if (!limits.IsKnown)
            return NO_IMAGE_LIMIT;

        var parts = new List<string>(2);
        if (limits.MaxPerMessage is { } perMessage)
            parts.Add($"{perMessage.ToString(CultureInfo.InvariantCulture)} per message");

        if (limits.MaxPerRequest is { } perRequest)
            parts.Add($"{perRequest.ToString(CultureInfo.InvariantCulture)} per request");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Writes a context window the way a snapshot line does.
    /// </summary>
    /// <remarks>
    /// Plain digits rather than thousands separators: the number is read by whoever reviews the
    /// diff, and a separator would make the file depend on which machine generated it.
    /// </remarks>
    /// <param name="window">The window to write.</param>
    /// <returns>The window, or a marker when nobody stated one.</returns>
    public static string Describe(ContextWindow window)
    {
        if (!window.IsKnown)
            return NO_WINDOW;

        return window.RaisableToTokens is { } raisable
            ? $"{window.DefaultTokens} up to {raisable}"
            : window.DefaultTokens.ToString(CultureInfo.InvariantCulture);
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