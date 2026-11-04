using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings.DataModel;

public static class ThemesExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ThemesExtensions).Namespace, nameof(ThemesExtensions));
    
    extension(Themes theme)
    {
        public string GetName() => theme switch
        {
            Themes.SYSTEM => TB("Synchronized with the operating system settings"),
            Themes.LIGHT => TB("Always use light theme"),
            Themes.DARK => TB("Always use dark theme"),

            _ => TB("Unknown setting"),
        };
    }
}