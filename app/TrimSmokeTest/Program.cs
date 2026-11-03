using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// Checks whether a trimmed build can still read and write the JSON of AI Studio, see TrimSmokeTest.csproj.
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

        var failures = new List<string>();
        foreach (var root in JsonRoots.ALL)
            failures.AddRange(JsonContractWalker.FindBrokenContracts(root));

        failures.AddRange(JsonRoundTrips.FindLosses());
        if (failures.Count is 0)
        {
            Console.WriteLine($"- All {JsonRoots.ALL.Count} JSON roots can be read and written.");
            return 0;
        }

        foreach (var failure in failures)
            Console.WriteLine($"- Error: {failure}");

        return 1;
    }
}