using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class ThemesExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ThemesExtensions).Namespace, nameof(ThemesExtensions));
    
    public static string GetName(this Themes theme) => theme switch
    {
        Themes.SYSTEM => TB("Synchronized with the operating system settings"),
        Themes.LIGHT => TB("Always use light theme"),
        Themes.DARK => TB("Always use dark theme"),

        _ => TB("Unknown setting"),
    };
}