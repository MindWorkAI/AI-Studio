// ReSharper disable NotAccessedPositionalProperty.Global

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Represents a file type filter for file selection dialogs.
/// </summary>
/// <param name="FilterName">The name of the filter.</param>
/// <param name="FilterExtensions">The file extensions associated with the filter.</param>
public readonly record struct FileTypeFilter(string FilterName, string[] FilterExtensions)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileTypeFilter).Namespace, nameof(FileTypeFilter));
    
    public static FileTypeFilter PDF => new(TB("PDF Files"), ["pdf"]);
    
    public static FileTypeFilter Text => new(TB("Text Files"), ["txt", "md"]);
    
    public static FileTypeFilter AllOffice => new(TB("All Office Files"), ["docx", "xlsx", "pptx", "doc", "xls", "ppt", "pdf"]);
    
    public static FileTypeFilter AllImages => new(TB("All Image Files"), ["jpg", "jpeg", "png", "gif", "bmp", "tiff", "svg", "webp", "heic"]);
    
    public static FileTypeFilter AllVideos => new(TB("All Video Files"), ["mp4", "m4v", "avi", "mkv", "mov", "wmv", "flv", "webm"]);
    
    public static FileTypeFilter AllAudio => new(TB("All Audio Files"), ["mp3", "wav", "wave", "aac", "flac", "ogg", "m4a", "wma", "alac", "aiff", "m4b"]);
    
    public static FileTypeFilter AllSourceCode => new(TB("All Source Code Files"), 
        [
            // C#:
            "cs",
            
            // Java:
            "java",
            
            // Python:
            "py",
            
            // JavaScript/TypeScript:
            "js", "ts",
            
            // C/C++:
            "c", "cpp", "h", "hpp",
            
            // Ruby:
            "rb",
            
            // Go:
            "go",
            
            // Rust:
            "rs",
            
            // Lua:
            "lua",
            
            // PHP:
            "php",
            
            // HTML/CSS:
            "html", "css",
            
            // Swift/Kotlin:
            "swift", "kt",
            
            // Shell scripts:
            "sh", "bash",
            
            // Logging files:
            "log",
            
            // JSON/YAML/XML:
            "json", "yaml", "yml", "xml",
            
            // Config files:
            "ini", "cfg", "toml", "plist",
        ]);
    
    public static FileTypeFilter Executables => new(TB("Executable Files"), ["exe", "app", "bin", "appimage"]);
}