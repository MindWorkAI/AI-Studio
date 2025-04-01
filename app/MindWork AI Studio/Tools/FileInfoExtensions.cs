namespace AIStudio.Tools;

public static class FileInfoExtensions
{
    /// <summary>
    /// Returns the file size in human-readable format.
    /// </summary>
    /// <param name="fileInfo">The file info object.</param>
    /// <returns>The file size in human-readable format.</returns>
    public static string FileSize(this FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
            return "N/A";

        return fileInfo.Length.FileSize();
    }
}