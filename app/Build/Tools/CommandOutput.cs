namespace Build.Tools;

/// <summary>
/// What an external tool wrote, and how it ended.
/// </summary>
/// <remarks>
/// The text alone cannot tell whether a tool failed: a build may stop on a diagnostic which no filter
/// recognizes, or without writing any at all. Only the exit code answers that question reliably.
/// </remarks>
/// <param name="Text">Everything the tool wrote to its standard output and its standard error, line by line.</param>
/// <param name="ExitCode">The exit code of the tool, where anything but zero is a failure.</param>
public readonly record struct CommandOutput(string Text, int ExitCode);