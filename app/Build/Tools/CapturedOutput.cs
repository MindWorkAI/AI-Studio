namespace Build.Tools;

/// <summary>
/// What an external tool wrote to its standard output, and how it ended.
/// </summary>
/// <remarks>
/// The standard error of the tool went straight to the terminal. A tool which fails says why it failed there, so
/// whoever reads the terminal learns the reason from the tool itself.
/// </remarks>
/// <param name="Text">Everything the tool wrote to its standard output.</param>
/// <param name="ExitCode">The exit code of the tool, where anything but zero is a failure.</param>
public readonly record struct CapturedOutput(string Text, int ExitCode);