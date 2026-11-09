namespace Build.Tools;

/// <summary>
/// Ends the build script with an exit code, after the command has said what went wrong.
/// </summary>
/// <remarks>
/// A release runs several commands one after another, and a failure deep inside one of them has to end
/// the whole release, so that nothing is built on top of half-prepared metadata or broken artifacts.
/// Program.cs turns the exception into the exit code and prints nothing more: the command already
/// explained the failure, and a stack trace would only bury that explanation.
/// </remarks>
/// <param name="exitCode">The exit code of the build script, where anything but zero is a failure.</param>
public sealed class CommandExitedException(int exitCode) : Exception
{
    /// <summary>
    /// The exit code the build script ends with.
    /// </summary>
    public int ExitCode { get; } = exitCode;
}