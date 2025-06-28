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
    
    public static FileTypeFilter AllImages => new(TB("All Image Files"), ["jpg", "jpeg", "png", "gif", "bmp", "tiff"]);
    
    public static FileTypeFilter Executables => new(TB("Executable Files"), ["exe", "app", "bin", "appimage"]);
}