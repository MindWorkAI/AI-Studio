namespace AIStudio.Settings.DataModel;

public static class ThemesExtensions
{
    public static string GetName(this Themes theme)
    {
        return theme switch
        {
            Themes.SYSTEM => "Synchronized with the operating system settings",
            Themes.LIGHT => "Always use light theme",
            Themes.DARK => "Always use dark theme",
            
            _ => "Unknown setting",
        };
    }
}