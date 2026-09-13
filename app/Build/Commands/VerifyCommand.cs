using Build.Tools;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Build.Commands;

/// <summary>
/// The quality gate: one command, the same one locally and in the pipeline.
/// </summary>
/// <remarks>
/// Every check runs, even after one of them has failed. A gate which stops at the first failure
/// tells you one thing per run, and the next run costs the same minutes again -- while the point of
/// running the whole thing is to learn everything which is wrong in one go.
/// </remarks>
public sealed class VerifyCommand
{
    /// <summary>
    /// How the .NET app is named once it lies where Tauri expects it.
    /// </summary>
    private const string SIDECAR_PREFIX = "mindworkAIStudioServer-";

    [Command("verify", Description = "Run the quality gate: .NET tests, Rust tests, Clippy, and the model sources")]
    public async Task<int> Verify()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return 1;

        Console.WriteLine("==============================");
        Console.WriteLine("- Quality gate: every check runs, so that the first failure does not hide the next ...");

        var results = new List<(string What, int ExitCode)>
        {
            (".NET tests", await CommandRunner.RunAsync(Environment.GetTestsDirectory(), "dotnet", "test --nologo")),
        };

        var runtimeDirectory = Environment.GetRustRuntimeDirectory();
        if (HasSidecar())
        {
            results.Add(("Rust tests", await CommandRunner.RunAsync(runtimeDirectory, "cargo", "test")));
            results.Add(("Clippy", await CommandRunner.RunAsync(runtimeDirectory, "cargo", "clippy --all-targets -- -D warnings")));
        }
        else
        {
            //
            // Tauri's build script copies the .NET app in as a sidecar and refuses to run at all
            // while that file is missing, so nothing Rust compiles without it. Failing here would
            // be a trap rather than a gate: the way to produce the sidecar is `dotnet run build`,
            // and that command runs this gate first -- a fresh clone would never get past it.
            //
            Console.WriteLine("- Skipping the Rust tests and Clippy: the .NET sidecar is missing, and Tauri's build script needs it before anything Rust compiles.");
            Console.WriteLine("   Run 'dotnet run build --skip-verify' once. From then on, this part of the gate runs with the rest.");
        }

        results.Add(("Model sources", new VerifyModelsCommand().VerifyModels()));

        Console.WriteLine("==============================");
        Console.WriteLine("- Quality gate:");
        foreach (var (what, exitCode) in results)
            Console.WriteLine($"   - {what}: {(exitCode is 0 ? "passed" : $"failed, exit code {exitCode}")}");

        var failed = results.Count(result => result.ExitCode is not 0);
        if (failed is 0)
        {
            Console.WriteLine($"- All {results.Count} checks passed.");
            return 0;
        }

        Console.WriteLine($"- {failed} of {results.Count} checks failed.");
        return 1;
    }

    private static bool HasSidecar()
    {
        var distributionDirectory = Path.Combine(Environment.GetAIStudioDirectory(), "bin", "dist");
        return Directory.Exists(distributionDirectory) && Directory.EnumerateFiles(distributionDirectory, $"{SIDECAR_PREFIX}*").Any();
    }
}