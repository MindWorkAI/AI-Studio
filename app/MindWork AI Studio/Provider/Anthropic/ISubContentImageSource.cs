namespace AIStudio.Provider.Anthropic;

public interface ISubContentImageSource
{
    /// <summary>
    /// The type of the sub-content image.
    /// </summary>
    public SubContentImageType Type { get; }
}