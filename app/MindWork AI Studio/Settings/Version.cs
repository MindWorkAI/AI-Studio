namespace AIStudio.Settings;

/// <summary>
/// The version of the settings file. Allows us to upgrade the settings,
/// in case a new version is available.
/// </summary>
public enum Version
{
    UNKNOWN,
    V1,
}