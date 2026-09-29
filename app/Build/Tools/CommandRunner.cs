using System.ComponentModel;
using System.Diagnostics;

namespace Build.Tools;

/// <summary>
/// Runs one external tool and lets it write straight to the terminal.
/// </summary>
/// <remarks>
/// The output is deliberately not captured. A gate which swallows the output of a failing test run
/// and then prints "failed" leaves the person who has to fix it with nothing to go on, while the
/// tools it runs already say everything worth saying -- which test, which line, which lint.
/// </remarks>
public static class CommandRunner
{
    /// <summary>
    /// What a tool which could not be started at all reports.
    /// </summary>
    /// <remarks>
    /// Anything but zero counts as a failure, so the exact number matters only in that it is not
    /// one a tool would plausibly return itself.
    /// </remarks>
    public const int COULD_NOT_START = 127;

    /// <summary>
    /// Runs a tool and waits for it.
    /// </summary>
    /// <param name="workingDirectory">Where the tool should run.</param>
    /// <param name="fileName">The tool, as it is called on the PATH.</param>
    /// <param name="arguments">What to pass it.</param>
    /// <returns>The exit code of the tool, or COULD_NOT_START when it never ran.</returns>
    public static async Task<int> RunAsync(string workingDirectory, string fileName, string arguments)
    {
        Console.WriteLine($"- Running '{fileName} {arguments}' in '{workingDirectory}' ...");
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.WriteLine($"- Error: '{fileName}' did not start, and the system did not say why.");
                return COULD_NOT_START;
            }

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Win32Exception exception)
        {
            Console.WriteLine($"- Error: '{fileName}' could not be started: {exception.Message}");
            Console.WriteLine($"   Is '{fileName}' installed and on the PATH?");
            return COULD_NOT_START;
        }
    }
}