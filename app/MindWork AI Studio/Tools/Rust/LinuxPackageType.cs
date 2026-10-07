namespace AIStudio.Tools.Rust;

/// <summary>
/// Identifies how the Linux build was packaged.
/// </summary>
public enum LinuxPackageType
{
    /// <summary>An unknown or future Linux package type reported by the runtime.</summary>
    UNKNOWN,

    /// <summary>The app is not running on Linux.</summary>
    NOT_APPLICABLE,

    /// <summary>An AppImage build.</summary>
    APP_IMAGE,

    /// <summary>A Flatpak build.</summary>
    FLATPAK,
}