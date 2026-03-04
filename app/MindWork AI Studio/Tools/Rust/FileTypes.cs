using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Central definition of supported file types with parent/child relationships and helpers
/// to build extension whitelists (e.g., for file pickers or validation).
/// </summary>
public static class FileTypes
{
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(FileType).Namespace, nameof(FileType));
    
    // Source code hierarchy: SourceCode -> (.NET, Java, Python, Web, C/C++, Config, ...)
    public static readonly FileType DOTNET     = FileType.Leaf(".NET", "cs", "razor", "vb", "fs", "aspx", "cshtml", "csproj");
    public static readonly FileType JAVA       = FileType.Leaf("Java", "java");
    public static readonly FileType PYTHON     = FileType.Leaf("Python", "py");
    public static readonly FileType JAVASCRIPT = FileType.Leaf("JavaScript/TypeScript", "js", "ts");
    public static readonly FileType CFAMILY    = FileType.Leaf("C/C++", "c", "cpp", "h", "hpp");
    public static readonly FileType RUBY       = FileType.Leaf("Ruby", "rb");
    public static readonly FileType GO         = FileType.Leaf("Go", "go");
    public static readonly FileType RUST       = FileType.Leaf("Rust", "rs");
    public static readonly FileType LUA        = FileType.Leaf("Lua", "lua");
    public static readonly FileType PHP        = FileType.Leaf("PHP", "php");
    public static readonly FileType WEB        = FileType.Leaf("HTML/CSS", "html", "css");
    public static readonly FileType APP        = FileType.Leaf("Swift/Kotlin", "swift", "kt");
    public static readonly FileType SHELL      = FileType.Leaf("Shell", "sh", "bash", "zsh");
    public static readonly FileType LOG        = FileType.Leaf("Log", "log");
    public static readonly FileType JSON       = FileType.Leaf("JSON", "json");
    public static readonly FileType XML        = FileType.Leaf("XML", "xml");
    public static readonly FileType YAML       = FileType.Leaf("YAML", "yaml", "yml");
    public static readonly FileType CONFIG     = FileType.Leaf(TB("Config"), "ini", "cfg", "toml", "plist");
    
    public static readonly FileType SOURCE_CODE = FileType.Parent(TB("Source Code"),
        DOTNET, JAVA, PYTHON, JAVASCRIPT, CFAMILY, RUBY, GO, RUST, LUA, PHP, WEB, APP, SHELL, LOG, JSON, XML, YAML, CONFIG);

    // Document hierarchy
    public static readonly FileType PDF         = FileType.Leaf("PDF", "pdf");
    public static readonly FileType TEXT        = FileType.Leaf(TB("Text"), "txt", "md");
    public static readonly FileType MS_WORD     = FileType.Leaf("Microsoft Word", "docx");
    public static readonly FileType WORD        = FileType.Composite("Word", ["docx"], MS_WORD);
    public static readonly FileType EXCEL       = FileType.Leaf("Excel", "xls", "xlsx");
    public static readonly FileType POWER_POINT = FileType.Leaf("PowerPoint", "ppt", "pptx");
    
    public static readonly FileType OFFICE_FILES = FileType.Parent(TB("Office Files"),
        WORD, EXCEL, POWER_POINT, PDF);
    public static readonly FileType DOCUMENT     = FileType.Parent(TB("Document"),
        TEXT, OFFICE_FILES, SOURCE_CODE);

    // Media hierarchy
    public static readonly FileType IMAGE = FileType.Leaf(TB("Image"),
        "jpg", "jpeg", "png", "gif", "bmp", "tiff", "svg", "webp", "heic");
    public static readonly FileType AUDIO = FileType.Leaf(TB("Audio"),
        "mp3", "wav", "wave", "aac", "flac", "ogg", "m4a", "wma", "alac", "aiff", "m4b");
    public static readonly FileType VIDEO = FileType.Leaf(TB("Video"),
        "mp4", "m4v", "avi", "mkv", "mov", "wmv", "flv", "webm");
    
    public static readonly FileType MEDIA = FileType.Parent(TB("Media"), IMAGE, AUDIO, VIDEO);

    // Other standalone types
    public static readonly FileType EXECUTABLES = FileType.Leaf(TB("Executable"), "exe", "app", "bin", "appimage");

    /// <summary>
    /// Builds a distinct, lower-cased list of extensions allowed for the provided types.
    /// Accepts both composite types (e.g., Document) and leaves (e.g., Pdf).
    /// </summary>
    public static string[] OnlyAllowTypes(params FileType[] types)
    {
        if (types.Length == 0)
            return [];

        return types
            .SelectMany(t => t.FlattenExtensions())
            .Select(ext => ext.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static FileType? AsOneFileType(params FileType[]? types)
    {
        if (types == null || types.Length == 0)
            return null;
        return FileType.Composite(TB("Custom"), OnlyAllowTypes(types));
    }
}
