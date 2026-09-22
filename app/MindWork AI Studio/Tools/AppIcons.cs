namespace AIStudio.Tools;

/// <summary>
/// Icons we draw ourselves, because the Material icon set MudBlazor ships does not contain them.
/// </summary>
/// <remarks>
/// The strings follow the same convention as the MudBlazor icons: they contain the SVG child
/// elements only, drawn on a 24 by 24 canvas. MudIcon and every component taking an icon wrap them
/// into the svg element themselves, which is why there must be no svg root element here.
/// </remarks>
public static class AppIcons
{
    /// <summary>
    /// The classic database symbol: a cylinder made of three stacked discs.
    /// </summary>
    public const string DATABASE =
        """
        <path d="M5 4.6A7 2.6 0 0 1 19 4.6L19 8.9A7 2.6 0 0 1 5 8.9Z"/><path d="M5 9.9A7 2.6 0 0 0 19 9.9L19 14.1A7 2.6 0 0 1 5 14.1Z"/><path d="M5 15.1A7 2.6 0 0 0 19 15.1L19 19.4A7 2.6 0 0 1 5 19.4Z"/>
        """;
}