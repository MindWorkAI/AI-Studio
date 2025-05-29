namespace AIStudio.Tools;

public readonly record struct DataWarningMessage(string Icon, string Message)
{
    public void Show(ISnackbar snackbar)
    {
        var icon = this.Icon;
        snackbar.Add(this.Message, Severity.Warning, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.HideTransitionDuration = 600;
            config.VisibleStateDuration = 12_000;
        });
    }
}