using AIStudio.Components;

namespace AIStudio.Tools;

/// <summary>
/// The notification that a file is being exported.
/// </summary>
/// <remarks>
/// It stays until the export ends, however long that takes, since a timeout would claim an end which
/// has not happened. Closing it is allowed: the export goes on all the same.
/// </remarks>
/// <param name="Key">The key of the export, shared with its success notification, which replaces this one.</param>
/// <param name="FilePath">The file the export writes.</param>
public readonly record struct DataFileExportRunningMessage(string Key, string FilePath)
{
    public void Show(ISnackbar snackbar)
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(FileExportRunningMessage.FileName)] = Path.GetFileName(this.FilePath)
        };

        snackbar.Add<FileExportRunningMessage>(parameters, Severity.Info, config =>
        {
            config.HideIcon = true;
            config.RequireInteraction = true;
            config.HideTransitionDuration = 600;
        }, this.Key);
    }

    public void Hide(ISnackbar snackbar) => snackbar.RemoveByKey(this.Key);
}