namespace AIStudio.Dialogs;

public static class DialogOptions
{
    public static readonly MudBlazor.DialogOptions FULLSCREEN = new()
    {
        CloseOnEscapeKey = true,
        FullWidth = true, MaxWidth = MaxWidth.Medium,
    };

    /// <summary>
    /// For the settings window: wider than <see cref="FULLSCREEN"/>, since a list stands beside its
    /// content, and closed with the × in its title instead of a button below. Every change in the window
    /// applies at once, so there is nothing to confirm, and the list and the content reach down to the
    /// bottom of the window.
    /// </summary>
    public static readonly MudBlazor.DialogOptions SETTINGS_WINDOW = new()
    {
        CloseOnEscapeKey = true,
        CloseButton = true,
        FullWidth = true, MaxWidth = MaxWidth.Large,
    };

    public static readonly MudBlazor.DialogOptions FULLSCREEN_MANUAL_ESCAPE = new()
    {
        CloseOnEscapeKey = false,
        FullWidth = true, MaxWidth = MaxWidth.Medium,
    };
    
    public static readonly MudBlazor.DialogOptions FULLSCREEN_NO_HEADER = new()
    {
        NoHeader = true,
        CloseOnEscapeKey = true,
        FullWidth = true, MaxWidth = MaxWidth.Medium,
    };

    public static readonly MudBlazor.DialogOptions BLOCKING_FULLSCREEN = new()
    {
        BackdropClick = false,
        CloseOnEscapeKey = false,
        FullWidth = true, MaxWidth = MaxWidth.Medium,
    };
}