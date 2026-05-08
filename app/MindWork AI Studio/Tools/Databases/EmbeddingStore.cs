namespace AIStudio.Tools.Databases;

public abstract class EmbeddingStore(string name, string path)
{
    public string Name => name;

    public virtual bool IsAvailable => true;
    
    private string Path => path;

    private ILogger<EmbeddingStore>? logger;
    
    public abstract IAsyncEnumerable<(string Label, string Value)> GetDisplayInfo();

    protected string GetStorageSize()
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

    private static string FormatBytes(long size)
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
    
    public void SetLogger(ILogger<EmbeddingStore> logService)
    {
        this.logger = logService;
    }
    

    public abstract Task EnsureEmbeddingStoreExists(string collectionName, int vectorSize, CancellationToken token);

    public abstract Task InsertEmbedding(string collectionName, IReadOnlyList<EmbeddingStoragePoint> points, CancellationToken token);

    public abstract Task DeleteEmbeddingByFile(string collectionName, string filePath, CancellationToken token);

    public abstract Task DeleteEmbeddingStore(string collectionName, CancellationToken token);

    public abstract void Dispose();
}