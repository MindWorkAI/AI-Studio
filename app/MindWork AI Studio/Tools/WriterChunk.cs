namespace AIStudio.Tools;

public sealed class WriterChunk(ReadOnlyMemory<char> content, bool isSelected, bool isProcessing)
{
    public ReadOnlyMemory<char> Content = content;
    
    public bool IsSelected = isSelected;
    
    public bool IsProcessing = isProcessing;
}