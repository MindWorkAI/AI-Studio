using AIStudio.Settings;

namespace AIStudio.Tools;

public static class MudThemeExtensions
{
    public static Palette GetCurrentPalette(this MudTheme theme, SettingsManager settingsManager) => settingsManager.IsDarkMode switch
    {
        true => theme.PaletteDark,
        false => theme.PaletteLight,
    };

    public static string GetActivityIndicatorColor(this MudTheme theme, SettingsManager settingsManager) => settingsManager.IsDarkMode switch
    {
        true => theme.GetActivityIndicatorDarkColor(),
        false => theme.GetActivityIndicatorLightColor(),
    };

    public static string GetActivityIndicatorLightColor(this MudTheme theme) => theme.PaletteLight.Info.Value;

    public static string GetActivityIndicatorDarkColor(this MudTheme theme) => theme.PaletteDark.InfoLighten;
}