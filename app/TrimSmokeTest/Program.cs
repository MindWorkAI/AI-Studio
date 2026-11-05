using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Checks whether a trimmed build can still read and write the JSON of AI Studio, and compute with the
/// Mathematics tools, see TrimSmokeTest.csproj.
/// </summary>
public static class Program
{
    public static int Main()
    {
        //
        // Several types of the app initialize a static logger from this factory, which only the start of
        // the app assigns. Without it, they die in their type initializer, see app/Tests/TestHost.cs:
        //
        AIStudio.Program.LOGGER_FACTORY = NullLoggerFactory.Instance;

        var roots = JsonRoots.FindAll();
        var failures = new List<string>();

        //
        // Finding no options at all means looking in the wrong place, not a build without JSON:
        //
        if (roots.Count is 0)
            failures.Add("Found no JSON options of AI Studio, see AppJsonOptions.FindAll, so nothing got checked.");

        foreach (var root in roots)
            failures.AddRange(JsonContractWalker.FindBrokenContracts(root));

        failures.AddRange(JsonRoundTrips.FindLosses());
        failures.AddRange(MathematicsChecks.FindFailures());
        if (failures.Count is 0)
        {
            Console.WriteLine($"- All {roots.Count} JSON roots can be read and written.");
            Console.WriteLine("- The Mathematics tools compute what they compute untrimmed.");
            return 0;
        }

        foreach (var failure in failures)
            Console.WriteLine($"- Error: {failure}");

        return 1;
    }
}