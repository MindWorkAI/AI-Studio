namespace AIStudio.Tools.Databases;

public abstract class DatabaseClient
{
    public string Name { get; }
    private string Path { get; }

    public DatabaseClient(string name, string path)
    {
        this.Name = name;
        this.Path = path;
    }
    
    public abstract IEnumerable<(string Label, string Value)> GetDisplayInfo();

    public string GetStorageSize()
    {
        if (string.IsNullOrEmpty(this.Path))
        {
            Console.WriteLine($"Error: Database path '{this.Path}' cannot be null or empty.");
            return "0 B";
        }

        if (!Directory.Exists(this.Path))
        {
            Console.WriteLine($"Error: Database path '{this.Path}' does not exist.");
            return "0 B";
        }
        long size = 0;
        var stack = new Stack<string>();
        stack.Push(this.Path);
        while (stack.Count > 0)
        {
            string directory = stack.Pop();
            try
            {
                var files = Directory.GetFiles(directory);
                size += files.Sum(file => new FileInfo(file).Length);
                var subDirectories = Directory.GetDirectories(directory);
                foreach (var subDirectory in subDirectories)
                {
                    stack.Push(subDirectory);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"No access to {directory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error encountered while processing {directory}: ");
                Console.WriteLine($"{ ex.Message}");
            }
        }
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
}