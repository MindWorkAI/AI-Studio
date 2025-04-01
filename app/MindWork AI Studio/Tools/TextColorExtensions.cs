using AIStudio.Settings;

namespace AIStudio.Tools;

public static class TextColorExtensions
{
    public static string GetHTMLColor(this TextColor color, SettingsManager settingsManager) => color switch
    {
        TextColor.DEFAULT => string.Empty,
        
        TextColor.ERROR => settingsManager.IsDarkMode ? "#ff6c6c" : "#ff0000",
        TextColor.WARN => settingsManager.IsDarkMode ? "#c7a009" : "#c7c000",
        TextColor.SUCCESS => settingsManager.IsDarkMode ? "#08b342" : "#009933",
        TextColor.INFO => settingsManager.IsDarkMode ? "#5279b8" : "#2d67c4",
        
        _ => string.Empty,
    };
}