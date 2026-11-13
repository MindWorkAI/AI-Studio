namespace AIStudio.Tools.Rust;

public sealed class SelectFileOptions
{
    public required string Title { get; init; }
    
    public PreviousFile? PreviousFile { get; init; }

    public FileDialogFilter? Filter { get; init; }
}