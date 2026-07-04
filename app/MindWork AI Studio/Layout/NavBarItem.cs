using AIStudio.Settings;

namespace AIStudio.Layout;

public record NavBarItem(string Name, string Icon, string IconLightColor, string IconDarkColor, string Path, bool MatchAll)
{
    /// <summary>
    /// Gets the CSS style that applies the current theme-aware icon color.
    /// </summary>
    /// <param name="settingsManager">The settings manager used to read the current theme.</param>
    /// <returns>The CSS style for the nav item icon color.</returns>
    public string SetColorStyle(SettingsManager settingsManager) => $"--custom-icon-color: {(settingsManager.IsDarkMode ? this.IconDarkColor : this.IconLightColor)};";
}