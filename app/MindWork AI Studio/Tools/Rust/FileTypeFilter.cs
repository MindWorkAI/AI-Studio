// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Tools.Rust;

/// <summary>
/// Represents a file type filter for file selection dialogs.
/// </summary>
/// <param name="FilterName">The name of the filter.</param>
/// <param name="FilterExtensions">The file extensions associated with the filter.</param>
public readonly record struct FileTypeFilter(string FilterName, string[] FilterExtensions)
{
    public static FileTypeFilter PDF => new("PDF Files", ["pdf"]);
    
    public static FileTypeFilter Text => new("Text Files", ["txt", "md"]);
    
    public static FileTypeFilter AllOffice => new("All Office Files", ["docx", "xlsx", "pptx", "doc", "xls", "ppt", "pdf"]);
    
    public static FileTypeFilter AllImages => new("All Image Files", ["jpg", "jpeg", "png", "gif", "bmp", "tiff"]);
}