using AIStudio.Components;

namespace AIStudio.Tools;

/// <summary>
/// The notification that a file was exported, with the ways to reach it.
/// </summary>
/// <remarks>
/// By default, it stays until the user closes it, also after they took one of its ways: whoever exports
/// a file wants to get to it, often in both ways, and that may well happen later than a timeout would
/// allow. Users who prefer it otherwise let it close like other messages, see
/// DataApp.CloseExportMessagesAutomatically.
/// </remarks>
/// <param name="Key">The key of the export. The notification that the export runs has the same one and gets replaced.</param>
/// <param name="Icon">The icon of the notification.</param>
/// <param name="Message">What was exported.</param>
/// <param name="FilePath">The file which was written.</param>
/// <param name="OffersOpening">Whether the file may be opened with the default program. False for an
/// archive, which that program would only unpack, at least on macOS.</param>
public readonly record struct DataFileExportSuccessMessage(string Key, string Icon, string Message, string FilePath, bool OffersOpening)
{
    /// <param name="snackbar">The notifications of the window.</param>
    /// <param name="closesAutomatically">Whether the notification closes after a few seconds, like other success messages.</param>
    public void Show(ISnackbar snackbar, bool closesAutomatically)
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(FileExportSuccessMessage.Message)] = this.Message,
            [nameof(FileExportSuccessMessage.FilePath)] = this.FilePath,
            [nameof(FileExportSuccessMessage.OffersOpening)] = this.OffersOpening
        };

        var icon = this.Icon;
        snackbar.RemoveByKey(this.Key);
        snackbar.Add<FileExportSuccessMessage>(parameters, Severity.Success, config =>
        {
            config.Icon = icon;
            config.IconSize = Size.Large;
            config.RequireInteraction = !closesAutomatically;
            config.VisibleStateDuration = 10_000;
            config.HideTransitionDuration = 600;
        }, this.Key);
    }
}