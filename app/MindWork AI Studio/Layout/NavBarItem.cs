using AIStudio.Settings;

namespace AIStudio.Layout;

/// <param name="NeedsAttention">Whether something behind this item waits for the user, such as a tool call to confirm. The item then wears a warning badge.</param>
public record NavBarItem(string Name, string Icon, string IconLightColor, string IconDarkColor, string Path, bool MatchAll, bool NeedsAttention = false)
{
    /// <summary>
    /// Gets the CSS style that applies the current theme-aware icon color.
    /// </summary>
    /// <param name="settingsManager">The settings manager used to read the current theme.</param>
    /// <returns>The CSS style for the nav item icon color.</returns>
    public string SetColorStyle(SettingsManager settingsManager) => $"--custom-icon-color: {(settingsManager.IsDarkMode ? this.IconDarkColor : this.IconLightColor)};";
}