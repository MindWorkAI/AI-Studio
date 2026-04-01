namespace AIStudio.Settings.DataModel;

public static class StartPageExtensions
{
    public static string ToRoute(this StartPage startPage) => startPage switch
    {
        StartPage.HOME => string.Empty,
        StartPage.CHAT => Routes.CHAT,
        StartPage.ASSISTANTS => Routes.ASSISTANTS,
        StartPage.INFORMATION => Routes.ABOUT,
        StartPage.PLUGINS => Routes.PLUGINS,
        StartPage.SUPPORTERS => Routes.SUPPORTERS,
        StartPage.SETTINGS => Routes.SETTINGS,
        
        _ => string.Empty,
    };
}