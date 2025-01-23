namespace AIStudio.Tools;

public static class DirectoryInfoExtensions
{
    private static readonly EnumerationOptions ENUMERATION_OPTIONS = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        ReturnSpecialDirectories = false,
    };

    /// <summary>
    /// Determines the size of the directory and all its subdirectories, as well as the number of files. When desired,
    /// it can report the found files up to a certain limit.
    /// </summary>
    /// <remarks>
    /// You might set reportMaxFiles to a negative value to report all files. Any positive value will limit the number
    /// of reported files. The cancellation token can be used to stop the operation. The cancellation operation is also able
    /// to cancel slow operations, e.g., when the directory is on a slow network drive.
    /// 
    /// After stopping the operation, the total size and number of files are reported as they were at the time of cancellation.
    /// 
    /// Please note that the entire operation is done on a background thread. Thus, when reporting the found files or the
    /// current total size, you need to use the appropriate dispatcher to update the UI. Usually, you can use the InvokeAsync
    /// method to update the UI from a background thread.
    /// </remarks>
    /// <param name="directoryInfo">The root directory to determine the size of.</param>
    /// <param name="reportCurrentTotalSize">The callback to report the current total size of the directory.</param>
    /// <param name="reportCurrentNumFiles">The callback to report the current number of files found.</param>
    /// <param name="reportNextFile">The callback to report the next file found. The file name is relative to the root directory.</param>
    /// <param name="reportMaxFiles">The maximum number of files to report. A negative value reports all files.</param>
    /// <param name="done">The callback to report that the operation is done.</param>
    /// <param name="cancellationToken">The cancellation token to stop the operation.</param>
    public static async Task DetermineContentSize(this DirectoryInfo directoryInfo, Action<long> reportCurrentTotalSize, Action<long> reportCurrentNumFiles, Action<string> reportNextFile, int reportMaxFiles = -1, Action? done = null, CancellationToken cancellationToken = default)
    {
        var rootDirectoryLen = directoryInfo.FullName.Length;
        long totalSize = 0;
        long numFiles = 0;
        
        await Task.Factory.StartNew(() => {
            foreach (var file in directoryInfo.EnumerateFiles("*", ENUMERATION_OPTIONS))
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                totalSize += file.Length;
                numFiles++;

                if (numFiles % 100 == 0)
                {
                    reportCurrentTotalSize(totalSize);
                    reportCurrentNumFiles(numFiles);
                }

                if (reportMaxFiles < 0 || numFiles <= reportMaxFiles)
                    reportNextFile(file.FullName[rootDirectoryLen..]);
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        reportCurrentTotalSize(totalSize);
        reportCurrentNumFiles(numFiles);
        
        if(done is not null)
            done();
    }
}