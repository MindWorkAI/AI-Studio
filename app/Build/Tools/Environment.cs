using System.Runtime.InteropServices;

using SharedTools;

namespace Build.Tools;

public static class Environment
{
    public const string DOTNET_VERSION = "net9.0";
    public static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(false);
    
    private static readonly Dictionary<RID, string> ALL_RIDS = Enum.GetValues<RID>().Select(rid => new KeyValuePair<RID, string>(rid, rid.AsMicrosoftRid())).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    
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
    
    public static string GetRustRuntimeDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = Path.Combine(currentDirectory, "..", "..", "runtime");
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
    
    public static IEnumerable<RID> GetRidsForCurrentOS()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ALL_RIDS.Where(rid => rid.Value.StartsWith("win-", StringComparison.Ordinal)).Select(n => n.Key);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ALL_RIDS.Where(rid => rid.Value.StartsWith("osx-", StringComparison.Ordinal)).Select(n => n.Key);
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ALL_RIDS.Where(rid => rid.Value.StartsWith("linux-", StringComparison.Ordinal)).Select(n => n.Key);
        
        Console.WriteLine($"Error: Unsupported OS '{RuntimeInformation.OSDescription}'");
        return [];
    }
    
    public static RID GetCurrentRid()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return arch switch
            {
                Architecture.X64 => RID.WIN_X64,
                Architecture.Arm64 => RID.WIN_ARM64,
                
                _ => RID.NONE,
            };
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return arch switch
            {
                Architecture.X64 => RID.OSX_X64,
                Architecture.Arm64 => RID.OSX_ARM64,
                
                _ => RID.NONE,
            };
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return arch switch
            {
                Architecture.X64 => RID.LINUX_X64,
                Architecture.Arm64 => RID.LINUX_ARM64,
                
                _ => RID.NONE,
            };
        
        Console.WriteLine($"Error: Unsupported OS '{RuntimeInformation.OSDescription}'");
        return RID.NONE;
    }
}