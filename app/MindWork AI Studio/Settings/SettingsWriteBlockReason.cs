namespace AIStudio.Settings;

public enum SettingsWriteBlockReason
{
    NONE,
    VERSION_MISSING,
    VERSION_UNKNOWN,
    VERSION_NEWER_THAN_APP,
    FILE_UNREADABLE,
    CURRENT_VERSION_INVALID,
}