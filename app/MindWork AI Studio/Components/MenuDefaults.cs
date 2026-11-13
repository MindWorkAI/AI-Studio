namespace AIStudio.Components;

/// <summary>
/// Settings every menu of AI Studio shares.
/// </summary>
public static class MenuDefaults
{
    /// <summary>
    /// The height in pixels above which a menu scrolls.
    /// </summary>
    /// <remarks>
    /// MudBlazor keeps a MudSelect inside the window by itself, but never a MudMenu: a menu runs past the
    /// edge of the window, and its lower entries cannot be reached. A menu whose entries come from data, such
    /// as the assistants or the profiles, therefore sets MaxHeight to this value. It is the height MudBlazor
    /// gives a MudSelect, so both look alike.
    /// </remarks>
    public const int MAX_HEIGHT = 300;
}
