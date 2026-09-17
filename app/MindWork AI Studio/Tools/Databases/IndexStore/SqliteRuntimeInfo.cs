using System.Runtime.InteropServices;

namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// What the running process can tell about the SQLite library it has loaded.
/// </summary>
/// <remarks>
/// These are methods instead of static fields on purpose: SQLitePCL.Batteries_V2.Init() runs when the
/// index store client is created, and a type initializer could well run before that. Every member
/// answers with an empty string when it cannot tell, so a single unavailable detail never costs the
/// caller the rest of them.
/// </remarks>
internal static class SqliteRuntimeInfo
{
    /// <summary>
    /// The name of the native library SQLitePCLRaw has bound to.
    /// </summary>
    /// <remarks>
    /// We ship our own build through the bundle_e_sqlite3 package, so this reads e_sqlite3 on every
    /// platform. Anything else means the process bound to a different library than we shipped, which
    /// is exactly the kind of thing a support case needs to show.
    /// </remarks>
    public static string GetNativeLibraryName()
    {
        try
        {
            return SQLitePCL.raw.GetNativeLibraryName();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Where the native library sits on disk.
    /// </summary>
    /// <remarks>
    /// This probes the file system instead of enumerating the loaded modules: Process.Modules throws
    /// on macOS, and NativeLibrary hands out no path at all. For our single-file builds, the base
    /// directory is where the runtime extracts the native assets to, so that is the first candidate.
    /// </remarks>
    public static string GetNativeLibraryPath()
    {
        try
        {
            var libraryName = GetNativeLibraryName();
            if (string.IsNullOrWhiteSpace(libraryName))
                return string.Empty;

            var fileName = GetNativeFileName(libraryName);
            var baseDirectory = AppContext.BaseDirectory;
            string[] candidates =
            [
                Path.Combine(baseDirectory, fileName),
                Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", fileName),
            ];

            foreach (var candidate in candidates)
                if (File.Exists(candidate))
                    return candidate;

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// The version of the managed SQLitePCLRaw wrapper, which is a different thing than the SQLite version.
    /// </summary>
    public static string GetWrapperVersion()
    {
        try
        {
            // Read the assembly name rather than an attribute: reflecting over members would not
            // survive trimming, the name does.
            var wrapperVersion = typeof(SQLitePCL.raw).Assembly.GetName().Version;
            return wrapperVersion is null ? string.Empty : $"SQLitePCLRaw.core {wrapperVersion.ToString(3)}";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// The architecture this process runs as, together with the runtime identifier it was built for.
    /// </summary>
    public static string GetProcessArchitecture()
    {
        try
        {
            return $"{RuntimeInformation.ProcessArchitecture} ({RuntimeInformation.RuntimeIdentifier})";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// The architecture of the machine, but only when it differs from the one of the process.
    /// </summary>
    /// <remarks>
    /// A difference means the process runs through an emulation layer, Rosetta above all. That is a
    /// classic reason for a native library failing to load, so it earns its own line when it happens
    /// and stays out of the way when it does not.
    /// </remarks>
    public static string GetSystemArchitecture()
    {
        try
        {
            return RuntimeInformation.OSArchitecture == RuntimeInformation.ProcessArchitecture ? string.Empty : RuntimeInformation.OSArchitecture.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetNativeFileName(string libraryName)
    {
        if (OperatingSystem.IsWindows())
            return $"{libraryName}.dll";

        if (OperatingSystem.IsMacOS())
            return $"lib{libraryName}.dylib";

        return $"lib{libraryName}.so";
    }
}