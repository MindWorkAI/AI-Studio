using System.Text.RegularExpressions;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Build.Commands;

/// <summary>
/// Reports how long ago somebody last read the pages the model rules were written from.
/// </summary>
/// <remarks>
/// Everything a rule set can be asked about itself is asked by the test project, against the
/// registry as it is really built: whether two rules claim the same names with the same right,
/// whether every family and every host names a page and a day, whether every pattern is written the
/// way model names arrive, whether a family reaches for one of the three reasoning words, and
/// whether a rank was set without saying what it moves past. Those belong there and not here --
/// asking them a second time in this command would be a second implementation of the same
/// judgement, and two implementations of one judgement drift apart.
///
/// The one question a test cannot ask is this one, because its answer changes with the calendar
/// rather than with the code: a family nobody touched would turn red on some Tuesday six months
/// after it was written. That is why it reports instead of failing, and why it is a command of its
/// own rather than a test or an analyzer.
///
/// It fails on exactly one thing: when it can no longer read the sources at all. A check which
/// quietly reads nothing reports that everything is fine.
/// </remarks>
public sealed partial class VerifyModelsCommand
{
    /// <summary>
    /// How long a page may go unread before it is worth mentioning.
    /// </summary>
    private const int DEFAULT_MONTHS = 6;

    /// <summary>
    /// The part of a source statement which is there in every spelling of it.
    /// </summary>
    /// <remarks>
    /// Counting these and comparing the count with what the pattern below actually read is how this
    /// command notices that it has gone blind, rather than reporting an empty list of old sources.
    /// </remarks>
    private const string DAY_MARKER = "new DateOnly(";

    [Command("verify-models", Description = "Report how long ago the pages behind the model rules were read")]
    public int VerifyModels(
        [Option("months", Description = "How long a page may go unread before it is reported")] int months = DEFAULT_MONTHS)
    {
        if(!Environment.IsWorkingDirectoryValid())
            return 1;

        if (months < 1)
        {
            Console.WriteLine("- Error: The number of months has to be at least 1.");
            return 1;
        }

        var modelsDirectory = Path.Combine(Environment.GetAIStudioDirectory(), "Models");
        if (!Directory.Exists(modelsDirectory))
        {
            Console.WriteLine($"- Error: The models directory '{modelsDirectory}' does not exist. Either it moved, or this command looks in the wrong place.");
            return 1;
        }

        Console.WriteLine("==============================");
        Console.WriteLine("- Reading the sources behind the model rules ...");

        var repository = Environment.GetRepositoryDirectory();
        var files = Directory.EnumerateFiles(modelsDirectory, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        var sources = new List<ReadSource>();
        var unreadable = new List<string>();

        foreach (var file in files)
        {
            var place = RelativeTo(repository, file);
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var stated = CountOccurrences(line, DAY_MARKER);
                if (stated is 0)
                    continue;

                var read = SourceStatement().Matches(line);
                foreach (Match statement in read)
                    sources.Add(new(place, index + 1, statement.Groups["url"].Value, new(int.Parse(statement.Groups["year"].ValueSpan), int.Parse(statement.Groups["month"].ValueSpan), int.Parse(statement.Groups["day"].ValueSpan))));

                for (var missed = read.Count; missed < stated; missed++)
                    unreadable.Add($"{place}:{index + 1}");
            }
        }

        if (sources.Count is 0)
        {
            Console.WriteLine($"- Error: Not one source was found in the {files.Length} files under '{RelativeTo(repository, modelsDirectory)}'.");
            Console.WriteLine("   Every family and every host states one, so finding none means this command can no longer read them.");
            Console.WriteLine("   A check which reads nothing reports that everything is fine, which is why this is an error rather than an empty report.");
            return 1;
        }

        if (unreadable.Count > 0)
        {
            Console.WriteLine($"- Error: {unreadable.Count} source(s) are written in a shape this command cannot read:");
            foreach (var place in unreadable)
                Console.WriteLine($"   - {place}");

            Console.WriteLine("   A source whose day cannot be read never grows old, and would stay out of the report below without anybody noticing.");
            Console.WriteLine("""   Write it as new("<url>", new DateOnly(<year>, <month>, <day>), "<note>") on one line, or teach this command the new shape.""");
            return 1;
        }

        var oldest = sources.MinBy(source => source.CheckedOn);
        var newest = sources.MaxBy(source => source.CheckedOn);
        Console.WriteLine($"- Read {sources.Count} sources in {files.Length} files under '{RelativeTo(repository, modelsDirectory)}'.");
        Console.WriteLine($"   - Oldest: {oldest.CheckedOn:yyyy-MM-dd}, in {oldest.Place}:{oldest.Line}");
        Console.WriteLine($"   - Newest: {newest.CheckedOn:yyyy-MM-dd}, in {newest.Place}:{newest.Line}");

        var lastAcceptableDay = DateOnly.FromDateTime(DateTime.Today).AddMonths(-months);
        var stale = sources.Where(source => source.CheckedOn < lastAcceptableDay).OrderBy(source => source.CheckedOn).ToArray();
        if (stale.Length is 0)
        {
            Console.WriteLine($"- Every source was read on {lastAcceptableDay:yyyy-MM-dd} or later, so none of them is older than {months} months.");
            return 0;
        }

        var insideActions = string.Equals(global::System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"- {stale.Length} source(s) have not been read since {lastAcceptableDay:yyyy-MM-dd}:");
        foreach (var source in stale)
        {
            Console.WriteLine($"   - {source.Place}:{source.Line}, last read on {source.CheckedOn:yyyy-MM-dd}: {source.Url}");
            if (insideActions)
                Console.WriteLine($"::warning file={source.Place},line={source.Line}::This model source was last read on {source.CheckedOn:yyyy-MM-dd}: {source.Url}");
        }

        Console.WriteLine("- This is a report and never a failure: a page nobody has looked at for a while is not a page which changed.");
        return 0;
    }

    /// <summary>
    /// How a source statement is written, in the one spelling the whole model namespace uses.
    /// </summary>
    /// <remarks>
    /// Both the family and the host state it as a target-typed new, so the type name itself is
    /// nowhere in the line. What is always there is the page, then the day.
    /// </remarks>
    [GeneratedRegex("""new\("(?<url>[^"]*)",\s*new DateOnly\((?<year>\d{4}),\s*(?<month>\d{1,2}),\s*(?<day>\d{1,2})\)""")]
    private static partial Regex SourceStatement();

    private static int CountOccurrences(string line, string marker)
    {
        var found = 0;
        var at = line.IndexOf(marker, StringComparison.Ordinal);
        while (at >= 0)
        {
            found++;
            at = line.IndexOf(marker, at + marker.Length, StringComparison.Ordinal);
        }

        return found;
    }

    /// <summary>
    /// A path as GitHub reads it: relative to the checkout, with forward slashes.
    /// </summary>
    /// <remarks>
    /// An annotation carrying an absolute path of somebody's machine lands nowhere, and it does so
    /// without saying that it did.
    /// </remarks>
    private static string RelativeTo(string repository, string path) => Path.GetRelativePath(repository, path).Replace('\\', '/');

    /// <summary>
    /// One page a rule was written from, and the day somebody last read it.
    /// </summary>
    /// <param name="Place">The file it is stated in, relative to the repository.</param>
    /// <param name="Line">The line it is stated on.</param>
    /// <param name="Url">The page.</param>
    /// <param name="CheckedOn">The day somebody last read it.</param>
    private readonly record struct ReadSource(string Place, int Line, string Url, DateOnly CheckedOn);
}