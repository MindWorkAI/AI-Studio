using AIStudio.Settings;

namespace AIStudio.Tools;

public static class MudThemeExtensions
{
    public static Palette GetCurrentPalette(this MudTheme theme, SettingsManager settingsManager) => settingsManager.IsDarkMode switch
    {
        true => theme.PaletteDark,
        false => theme.PaletteLight,
    };
}