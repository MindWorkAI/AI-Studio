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
            true => theme.GetActivityIndicatorDarkColor(),
            false => theme.GetActivityIndicatorLightColor(),
        };

        public string GetActivityIndicatorLightColor() => theme.PaletteLight.Info.Value;

        public string GetActivityIndicatorDarkColor() => theme.PaletteDark.InfoLighten;
    }
}