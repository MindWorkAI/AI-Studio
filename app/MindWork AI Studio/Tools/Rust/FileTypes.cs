using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Central definition of supported file types with parent/child relationships and helpers
/// to build extension whitelists (e.g., for file pickers or validation).
/// </summary>
public static class FileTypes
{
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(FileTypeFilter).Namespace, nameof(FileTypeFilter));

    // Keep SOURCE_LIKE in the same leaf style as the other file types.
    // These values are not sufficient for Dockerfile-style files without extensions,
    // therefore IsAllowedSourceLikeFileName is still required for real matching.
    public static readonly FileTypeFilter SOURCE_LIKE_FILE_NAMES = FileTypeFilter.Leaf(TB("Source like"),
        "Dockerfile", "Containerfile", "Jenkinsfile", "Makefile", "GNUmakefile", "Procfile", "Vagrantfile",
        "Tiltfile", "Justfile", "Brewfile", "Caddyfile", "Gemfile", "Podfile", "Fastfile", "Appfile", "Rakefile", "Dangerfile",
        "BUILD", "WORKSPACE", "BUCK");

    public static readonly FileTypeFilter SOURCE_LIKE_FILE_NAME_PREFIXES = FileTypeFilter.Leaf(TB("Source like prefix"),
        "Dockerfile", "Containerfile", "Jenkinsfile", "Procfile", "Caddyfile");
    
    // Source code hierarchy: SourceCode -> (.NET, Java, Python, Web, C/C++, Config, ...)
    public static readonly FileTypeFilter DOTNET     = FileTypeFilter.Leaf(".NET", "cs", "razor", "vb", "fs", "aspx", "cshtml", "csproj");
    public static readonly FileTypeFilter JAVA       = FileTypeFilter.Leaf("Java", "java");
    public static readonly FileTypeFilter PYTHON     = FileTypeFilter.Leaf("Python", "py");
    public static readonly FileTypeFilter JAVASCRIPT = FileTypeFilter.Leaf("JavaScript/TypeScript", "js", "ts");
    public static readonly FileTypeFilter CFAMILY    = FileTypeFilter.Leaf("C/C++", "c", "cpp", "h", "hpp");
    public static readonly FileTypeFilter RUBY       = FileTypeFilter.Leaf("Ruby", "rb");
    public static readonly FileTypeFilter GO         = FileTypeFilter.Leaf("Go", "go");
    public static readonly FileTypeFilter RUST       = FileTypeFilter.Leaf("Rust", "rs");
    public static readonly FileTypeFilter LUA        = FileTypeFilter.Leaf("Lua", "lua");
    public static readonly FileTypeFilter PHP        = FileTypeFilter.Leaf("PHP", "php");
    public static readonly FileTypeFilter WEB        = FileTypeFilter.Leaf("HTML/CSS", "html", "css");
    public static readonly FileTypeFilter APP        = FileTypeFilter.Leaf("Swift/Kotlin", "swift", "kt");
    public static readonly FileTypeFilter SHELL      = FileTypeFilter.Leaf("Shell", "sh", "bash", "zsh");
    public static readonly FileTypeFilter LOG        = FileTypeFilter.Leaf("Log", "log");
    public static readonly FileTypeFilter JSON       = FileTypeFilter.Leaf("JSON", "json");
    public static readonly FileTypeFilter XML        = FileTypeFilter.Leaf("XML", "xml");
    public static readonly FileTypeFilter YAML       = FileTypeFilter.Leaf("YAML", "yaml", "yml");
    public static readonly FileTypeFilter CONFIG     = FileTypeFilter.Leaf(TB("Config"), "ini", "cfg", "toml", "plist");

    public static readonly FileTypeFilter SOURCE_CODE = FileTypeFilter.Parent(TB("Source Code"),
        DOTNET, JAVA, PYTHON, JAVASCRIPT, CFAMILY, RUBY, GO, RUST, LUA, PHP, WEB, APP, SHELL, LOG, JSON, XML, YAML, CONFIG, SOURCE_LIKE_FILE_NAMES, SOURCE_LIKE_FILE_NAME_PREFIXES);

    // Document hierarchy
    public static readonly FileTypeFilter PDF         = FileTypeFilter.Leaf("PDF", "pdf");
    public static readonly FileTypeFilter TEXT        = FileTypeFilter.Leaf(TB("Text"), "txt", "md");
    public static readonly FileTypeFilter MS_WORD     = FileTypeFilter.Leaf("Microsoft Word", "docx");
    public static readonly FileTypeFilter WORD        = FileTypeFilter.Composite("Word", ["doc"], MS_WORD);
    public static readonly FileTypeFilter EXCEL       = FileTypeFilter.Leaf("Excel", "xls", "xlsx");
    public static readonly FileTypeFilter POWER_POINT = FileTypeFilter.Leaf("PowerPoint", "ppt", "pptx");
    public static readonly FileTypeFilter MAIL        = FileTypeFilter.Leaf(TB("Mail"), "eml", "msg", "mbox");

    public static readonly FileTypeFilter OFFICE_FILES = FileTypeFilter.Parent(TB("Office Files"),
        WORD, EXCEL, POWER_POINT, PDF);
    public static readonly FileTypeFilter DOCUMENT     = FileTypeFilter.Parent(TB("Document"),
        TEXT, OFFICE_FILES, SOURCE_CODE, MAIL);

    // Media hierarchy
    public static readonly FileTypeFilter IMAGE = FileTypeFilter.Leaf(TB("Image"),
        "jpg", "jpeg", "png", "gif", "bmp", "tiff", "svg", "webp", "heic");
    public static readonly FileTypeFilter AUDIO = FileTypeFilter.Leaf(TB("Audio"),
        "mp3", "wav", "wave", "aac", "flac", "ogg", "m4a", "wma", "alac", "aiff", "m4b");
    public static readonly FileTypeFilter VIDEO = FileTypeFilter.Leaf(TB("Video"),
        "mp4", "m4v", "avi", "mkv", "mov", "wmv", "flv", "webm");

    public static readonly FileTypeFilter MEDIA = FileTypeFilter.Parent(TB("Media"), IMAGE, AUDIO, VIDEO);

    // Other standalone types
    public static readonly FileTypeFilter EXECUTABLES = FileTypeFilter.Leaf(TB("Executable"), "exe", "app", "bin", "appimage");
    
    public static FileTypeFilter? AsOneFileType(params FileTypeFilter[]? types)
    {
        if (types == null || types.Length == 0)
            return null;
        
        if (types.Length == 1) return types[0];

        return FileTypeFilter.Composite(TB("Custom"), OnlyAllowTypes(types));
    }
    
    public static string[] OnlyAllowTypes(params FileTypeFilter[] types)
    {
        if (types.Length == 0)
            return [];

        return types
            .Where(t => t != SOURCE_LIKE_FILE_NAMES && t != SOURCE_LIKE_FILE_NAME_PREFIXES)
            .SelectMany(t => t.FlattenExtensions())
            .Select(ext => ext.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Validates a file path against the provided filters.
    /// Supports extension-based matching and source-like file names (e.g. Dockerfile).
    /// </summary>
    public static bool IsAllowedPath(string filePath, params FileTypeFilter[]? types)
    {
        if (types == null || types.Length == 0 || string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath).TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension))
        {
            if (OnlyAllowTypes(types).Contains(extension, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (types.Any(t => t.ContainsType(SOURCE_LIKE_FILE_NAMES)))
        { 
            if (SOURCE_LIKE_FILE_NAMES.FilterExtensions.Contains(fileName)) return true;
        }
        
        if (types.Any(t => t.ContainsType(SOURCE_LIKE_FILE_NAME_PREFIXES))){
            if (SOURCE_LIKE_FILE_NAME_PREFIXES.FilterExtensions.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return true;
        }
        
        return false;
    }
}
