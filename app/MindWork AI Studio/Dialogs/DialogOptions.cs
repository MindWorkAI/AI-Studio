namespace AIStudio.Dialogs;

public static class DialogOptions
{
    public static readonly MudBlazor.DialogOptions FULLSCREEN = new()
    {
        CloseOnEscapeKey = true,
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