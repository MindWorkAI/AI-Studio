namespace AIStudio.Tools;

/// <param name="Icon">The icon of the notification.</param>
/// <param name="Message">The text of the notification.</param>
/// <param name="StaysUntilClosed">Whether the notification stays until the user closes it, for something which waits for the user.</param>
/// <param name="Key">Identifies the notification, so that it can be removed again with Event.HIDE_NOTIFICATION once what it reports is over; null for none.</param>
public readonly record struct DataWarningMessage(string Icon, string Message, bool StaysUntilClosed = false, string? Key = null)
{
    public void Show(ISnackbar snackbar)
    {
        var icon = this.Icon;
        var staysUntilClosed = this.StaysUntilClosed;
        snackbar.Add(this.Message, Severity.Warning, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.HideTransitionDuration = 600;
            config.VisibleStateDuration = 12_000;
            config.RequireInteraction = staysUntilClosed;
            if (staysUntilClosed)
                config.ShowCloseIcon = true;
        }, this.Key);
    }
}