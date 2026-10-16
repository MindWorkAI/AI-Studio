namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// How strictly a search engine should filter explicit results.
/// </summary>
/// <remarks>
/// Stored and configured by name. Search engines number these levels, but a number in a
/// configuration file tells an administrator nothing about what it does.
/// </remarks>
public enum SafeSearchPolicy
{
    OFF,
    MODERATE,
    STRICT,
}