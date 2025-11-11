namespace AIStudio.Tools.Rust;

public class SaveFileOptions
{
    public required string Title { get; init; }
    
    public PreviousFile? PreviousFile { get; init; }

    public FileTypeFilter? Filter { get; init; }
}