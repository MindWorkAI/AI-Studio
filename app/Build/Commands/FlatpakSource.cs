namespace Build.Commands;

/// <summary>
/// Which state of AI Studio goes into a Flatpak which the build command "flatpak" builds.
/// </summary>
public enum FlatpakSource
{
    NONE,

    MAIN,
    PULL_REQUEST,
}