using System.Reflection;

namespace AIStudio.Tools.Metadata;

/// <summary>
/// The .NET runtime which runs the app.
/// </summary>
/// <remarks>
/// Asked while the app runs instead of taken from metadata.txt: a release brings its own runtime,
/// and a development build runs on whichever runtime the developer installed.
/// </remarks>
public static class DotnetRuntimeVersion
{
    /// <summary>
    /// How many characters of the commit we show, as metadata.txt does.
    /// </summary>
    private const int COMMIT_LENGTH = 10;

    /// <summary>
    /// Gets the version of the runtime together with the commit it was built from, such as
    /// <c>10.0.14 (commit 0427b9abbe)</c>.
    /// </summary>
    public static string Current { get; } = Describe(Environment.Version, typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// Describes a version of the runtime together with the commit which the informational version
    /// of its core library names after a plus sign, such as <c>10.0.14+0427b9abbead3f…</c>.
    /// </summary>
    /// <param name="version">The version of the runtime.</param>
    /// <param name="informationalVersion">The informational version of the core library, if any.</param>
    /// <returns>The version with its commit, or the version alone when the commit is unknown.</returns>
    internal static string Describe(Version version, string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return version.ToString();

        var plusIndex = informationalVersion.IndexOf('+');
        var commit = plusIndex < 0 ? string.Empty : informationalVersion[(plusIndex + 1)..];
        return commit.Length < COMMIT_LENGTH ? version.ToString() : $"{version} (commit {commit[..COMMIT_LENGTH]})";
    }
}