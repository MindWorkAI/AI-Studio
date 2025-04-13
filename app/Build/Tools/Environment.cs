using System.Runtime.InteropServices;

namespace Build.Tools;

public static class Environment
{
    private static readonly string[] ALL_RIDS = ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-arm64", "osx-x64"];
    
    public static bool IsWorkingDirectoryValid()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var mainFile = Path.Combine(currentDirectory, "Program.cs");
        var projectFile = Path.Combine(currentDirectory, "Build Script.csproj");
        
        if (!currentDirectory.EndsWith("Build", StringComparison.Ordinal) || !File.Exists(mainFile) || !File.Exists(projectFile))
        {
            Console.WriteLine("The current directory is not a valid working directory for the build script. Go to the /app/Build directory within the git repository.");
            return false;
        }
        
        return true;
    }

    public static string GetAIStudioDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = Path.Combine(currentDirectory, "..", "MindWork AI Studio");
        return Path.GetFullPath(directory);
    }
    
    public static string GetMetadataPath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = Path.Combine(currentDirectory, "..", "..", "metadata.txt");
        return Path.GetFullPath(directory);
    }

    public static string? GetOS()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        
        if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "darwin";

        Console.WriteLine($"Error: Unsupported OS '{RuntimeInformation.OSDescription}'");
        return null;
    }
    
    public static IEnumerable<string> GetRidsForCurrentOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ALL_RIDS.Where(rid => rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase));
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ALL_RIDS.Where(rid => rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase));
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ALL_RIDS.Where(rid => rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase));
        
        Console.WriteLine($"Error: Unsupported OS '{RuntimeInformation.OSDescription}'");
        return [];
    }
}