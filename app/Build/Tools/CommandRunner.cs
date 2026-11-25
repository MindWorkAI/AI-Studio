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
                return ReportNotStarted(fileName);

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Win32Exception exception)
        {
            return ReportCouldNotStart(fileName, exception);
        }
    }

    /// <summary>
    /// Runs a tool with a list of arguments and waits for it.
    /// </summary>
    /// <remarks>
    /// Each argument reaches the tool unchanged, also when it contains spaces or quotes, because the system quotes
    /// it as the platform needs it. Paths which a user chose, such as an output folder, therefore need no quoting.
    /// </remarks>
    /// <param name="workingDirectory">Where the tool should run.</param>
    /// <param name="fileName">The tool, as it is called on the PATH.</param>
    /// <param name="arguments">What to pass it, one argument per entry.</param>
    /// <param name="writeInput">Writes the standard input of the tool, or null when the tool reads none. The input ends once the callback returns.</param>
    /// <returns>The exit code of the tool, or COULD_NOT_START when it never ran.</returns>
    public static async Task<int> RunAsync(string workingDirectory, string fileName, IReadOnlyList<string> arguments, Func<Stream, Task>? writeInput = null)
    {
        Console.WriteLine($"- Running '{fileName} {DescribeArguments(arguments)}' in '{workingDirectory}' ...");
        var startInfo = CreateStartInfo(workingDirectory, fileName, arguments);
        startInfo.RedirectStandardInput = writeInput is not null;

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return ReportNotStarted(fileName);

            if (writeInput is not null)
            {
                try
                {
                    await using var input = process.StandardInput.BaseStream;
                    await writeInput(input);
                }
                catch (IOException)
                {
                    //
                    // The tool closed its input early, which it does when it fails. Its exit code
                    // and what it wrote to the terminal tell more than the broken pipe could:
                    //
                }
            }

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Win32Exception exception)
        {
            return ReportCouldNotStart(fileName, exception);
        }
    }

    /// <summary>
    /// Runs a tool with a list of arguments and collects what it writes to its standard output.
    /// </summary>
    /// <remarks>
    /// Its standard error still goes to the terminal, so a failing tool explains itself there. The standard output
    /// stays clean for reading answers such as a commit hash, which a warning on the same stream would spoil.
    /// </remarks>
    /// <param name="workingDirectory">Where the tool should run.</param>
    /// <param name="fileName">The tool, as it is called on the PATH.</param>
    /// <param name="arguments">What to pass it, one argument per entry.</param>
    /// <returns>The standard output and the exit code of the tool; the exit code is COULD_NOT_START when it never ran.</returns>
    public static async Task<CapturedOutput> ReadOutputAsync(string workingDirectory, string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = CreateStartInfo(workingDirectory, fileName, arguments);
        startInfo.RedirectStandardOutput = true;

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return new(string.Empty, ReportNotStarted(fileName));

            var text = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new(text, process.ExitCode);
        }
        catch (Win32Exception exception)
        {
            return new(string.Empty, ReportCouldNotStart(fileName, exception));
        }
    }

    private static ProcessStartInfo CreateStartInfo(string workingDirectory, string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private static string DescribeArguments(IReadOnlyList<string> arguments) => string.Join(' ', arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument));

    private static int ReportNotStarted(string fileName)
    {
        Console.WriteLine($"- Error: '{fileName}' did not start, and the system did not say why.");
        return COULD_NOT_START;
    }

    private static int ReportCouldNotStart(string fileName, Win32Exception exception)
    {
        Console.WriteLine($"- Error: '{fileName}' could not be started: {exception.Message}");
        Console.WriteLine($"   Is '{fileName}' installed and on the PATH?");
        return COULD_NOT_START;
    }
}