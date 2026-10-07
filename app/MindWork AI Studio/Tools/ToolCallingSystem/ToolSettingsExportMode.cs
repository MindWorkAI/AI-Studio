namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// How firmly an exported tool setting applies to the people who receive the configuration plugin.
/// </summary>
/// <remarks>
/// Chosen per export, and it covers the ordinary settings only. A secret is always locked, no
/// matter which mode is picked, because a pre-filled secret is one the user may save as their
/// own — see the tool settings service for that rule. The minimum provider confidence is
/// likewise a fixed requirement.
/// </remarks>
public enum ToolSettingsExportMode
{
    /// <summary>
    /// The organization fixes the value: it goes into LockedToolSettings, the user cannot change
    /// it, and it is reapplied on every configuration update.
    /// </summary>
    LOCKED,

    /// <summary>
    /// The organization pre-fills the value: it goes into DefaultToolSettings, and a value the
    /// user saves afterwards wins over it.
    /// </summary>
    DEFAULT,
}