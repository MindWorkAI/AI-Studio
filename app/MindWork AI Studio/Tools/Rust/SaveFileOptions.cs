namespace AIStudio.Tools.Rust;

public class SaveFileOptions
{
    public required string Title { get; init; }
    
    public PreviousFile? PreviousFile { get; init; }

    public FileDialogFilter? Filter { get; init; }
}