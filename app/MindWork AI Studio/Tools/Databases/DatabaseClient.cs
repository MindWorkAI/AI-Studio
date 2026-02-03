namespace AIStudio.Tools.Databases;

public abstract class DatabaseClient(string name, string path)
{
    public string Name => name;
    
    private string Path => path;
    
    protected ILogger<DatabaseClient>? logger;
    
    public abstract IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo();

    public string GetStorageSize()
    {
        if (string.IsNullOrWhiteSpace(this.Path))
        {
            this.logger!.LogError($"Error: Database path '{this.Path}' cannot be null or empty.");
            return "0 B";
        }

        if (!Directory.Exists(this.Path))
        {
            this.logger!.LogError($"Error: Database path '{this.Path}' does not exist.");
            return "0 B";
        }
        var files = Directory.EnumerateFiles(this.Path, "*", SearchOption.AllDirectories)
            .Where(file => !System.IO.Path.GetDirectoryName(file)!.Contains("cert", StringComparison.OrdinalIgnoreCase));
        var size = files.Sum(file => new FileInfo(file).Length);
        return FormatBytes(size);
    }
    
    public static string FormatBytes(long size)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int suffixIndex = 0;
    
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
    
        return $"{size:0##} {suffixes[suffixIndex]}";
    }
    
    public void SetLogger(ILogger<DatabaseClient> logService)
    {
        this.logger = logService;
    }

    public abstract void Dispose();
}