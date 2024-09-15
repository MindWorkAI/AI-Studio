using AIStudio.Settings;

namespace AIStudio.Layout;

public record NavBarItem(string Name, string Icon, string IconLightColor, string IconDarkColor, string Path, bool MatchAll)
{
    public string SetColorStyle(SettingsManager settingsManager) => $"--custom-icon-color: {(settingsManager.IsDarkMode ? this.IconDarkColor : this.IconLightColor)};";
}