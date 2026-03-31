namespace AIStudio.Tools.Rust;

public sealed class SelectFileOptions
{
    public required string Title { get; init; }
    
    public PreviousFile? PreviousFile { get; init; }

    public FileTypeFilter? Filter { get; init; }
}