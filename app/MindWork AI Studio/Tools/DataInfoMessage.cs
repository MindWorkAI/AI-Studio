namespace AIStudio.Tools;

public readonly record struct DataInfoMessage(string Icon, string Message)
{
    public void Show(ISnackbar snackbar)
    {
        var icon = this.Icon;
        snackbar.Add(this.Message, Severity.Info, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.HideTransitionDuration = 600;
            config.VisibleStateDuration = 10_000;
        });
    }
}