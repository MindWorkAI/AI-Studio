namespace AIStudio.Tools;

public sealed class Slide
{
    public bool Delivered { get; set; }

    public int Position { get; init; }
    
    public List<ISlideContent> Content { get; } = new();
}