namespace AIStudio.Tools.Databases;

public abstract class DatabaseClient(string name, string path)
{
    public string Name => name;

    public virtual string CacheKey => name;

    public virtual DatabaseClientStatus Status => DatabaseClientStatus.AVAILABLE;

    /// <summary>
    /// The version the running database reports about itself.
    /// </summary>
    /// <remarks>
    /// Empty when the client cannot tell. Callers which want to show a version in a headline read it
    /// from here instead of picking it out of the label-value pairs the display info yields.
    /// </remarks>
    public virtual string Version => string.Empty;

    public bool IsAvailable => this.Status is DatabaseClientStatus.AVAILABLE;
    
    private string Path => path;

    protected ILogger<DatabaseClient>? Logger;
    
    public abstract IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo();

    protected string GetStorageSize()
    {
        if (string.IsNullOrWhiteSpace(this.Path))
        {
            this.Logger!.LogError($"Error: Database path '{this.Path}' cannot be null or empty.");
            return "0 B";
        }

        if (!Directory.Exists(this.Path))
        {
            this.Logger!.LogError($"Error: Database path '{this.Path}' does not exist.");
            return "0 B";
        }
        var files = Directory.EnumerateFiles(this.Path, "*", SearchOption.AllDirectories)
            .Where(file => !System.IO.Path.GetDirectoryName(file)!.Contains("cert", StringComparison.OrdinalIgnoreCase));
        var size = files.Sum(file => new FileInfo(file).Length);
        return FormatBytes(size);
    }

    private static string FormatBytes(long size)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int suffixIndex = 0;
        double convertedSize = size;
    
        while (convertedSize >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            convertedSize /= 1024;
            suffixIndex++;
        }
    
        return $"{convertedSize:0.##} {suffixes[suffixIndex]}";
    }
    
    public void SetLogger(ILogger<DatabaseClient> logService)
    {
        this.Logger = logService;
    }

    public abstract void Dispose();
}