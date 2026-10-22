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
        if (WhatTauriExpectsIsThere())
        {
            results.Add(("Rust tests", await CommandRunner.RunAsync(runtimeDirectory, "cargo", "test")));
            results.Add(("Clippy", await CommandRunner.RunAsync(runtimeDirectory, "cargo", "clippy --all-targets -- -D warnings")));
        }
        else
        {
            //
            // Tauri's build script insists that everything the configuration lists is already
            // there and refuses to run otherwise, so nothing Rust compiles until a build has
            // produced those files once. Failing here would be a trap rather than a gate: the way
            // to produce them is `dotnet run build`, and that command runs this gate first -- a
            // fresh clone would never get past it.
            //
            Console.WriteLine("- Skipping the Rust tests and Clippy: the .NET sidecar or the downloaded libraries are missing, and Tauri's build script needs both before anything Rust compiles.");
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

    /// <summary>
    /// Whether a build has already produced the files Tauri's build script reads.
    /// </summary>
    /// <remarks>
    /// Both are products of a build rather than of the repository: the .NET app arrives as a
    /// sidecar, and the PDF library is downloaded into the resources. The other resource
    /// directories the configuration names are in the repository and are always there.
    /// </remarks>
    /// <returns>True, when cargo can get past the build script.</returns>
    private static bool WhatTauriExpectsIsThere()
    {
        var distributionDirectory = Path.Combine(Environment.GetAIStudioDirectory(), "bin", "dist");
        if (!Directory.Exists(distributionDirectory) || !Directory.EnumerateFiles(distributionDirectory, $"{SIDECAR_PREFIX}*").Any())
            return false;

        var librariesDirectory = Path.Combine(Environment.GetRustRuntimeDirectory(), "resources", "libraries");
        return Directory.Exists(librariesDirectory) && Directory.EnumerateFiles(librariesDirectory).Any();
    }
}