namespace AIStudio.Tools;

public static class FileInfoExtensions
{
    public static string FileSize(this FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
            return "N/A";

        var size = fileInfo.Length;
        string[] sizes = { "B", "kB", "MB", "GB", "TB" };
        var order = 0;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}