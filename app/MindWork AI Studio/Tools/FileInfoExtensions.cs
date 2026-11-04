namespace AIStudio.Tools;

public static class FileInfoExtensions
{
    /// <param name="fileInfo">The file info object.</param>
    extension(FileInfo fileInfo)
    {
        /// <summary>
        /// Returns the file size in human-readable format.
        /// </summary>
        /// <returns>The file size in human-readable format.</returns>
        public string FileSize()
        {
            if (!fileInfo.Exists)
                return "N/A";

            return fileInfo.Length.FileSize();
        }
    }
}