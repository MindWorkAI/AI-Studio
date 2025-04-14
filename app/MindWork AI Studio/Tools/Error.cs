namespace AIStudio.Tools;

public readonly record struct Error(string Icon, string Message)
{
    public void Show(ISnackbar snackbar)
    {
        var icon = this.Icon;
        snackbar.Add(this.Message, Severity.Error, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.HideTransitionDuration = 600;
            config.VisibleStateDuration = 14_000;
        });
    }
}

public readonly record struct Success(string Icon, string Message)
{
    public void Show(ISnackbar snackbar)
    {
        var icon = this.Icon;
        snackbar.Add(this.Message, Severity.Success, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.HideTransitionDuration = 600;
            config.VisibleStateDuration = 10_000;
        });
    }
}