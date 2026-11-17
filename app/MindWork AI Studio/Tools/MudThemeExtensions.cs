using AIStudio.Settings;

namespace AIStudio.Tools;

public static class MudThemeExtensions
{
    extension(MudTheme theme)
    {
        public Palette GetCurrentPalette(SettingsManager settingsManager) => settingsManager.IsDarkMode switch
        {
            true => theme.PaletteDark,
            false => theme.PaletteLight,
        };

        public string GetActivityIndicatorColor(SettingsManager settingsManager) => settingsManager.IsDarkMode switch
        {
            true => theme.ActivityIndicatorDarkColor,
            false => theme.ActivityIndicatorLightColor,
        };

        public string ActivityIndicatorLightColor => theme.PaletteLight.Info.Value;

        public string ActivityIndicatorDarkColor => theme.PaletteDark.InfoLighten;
    }
}