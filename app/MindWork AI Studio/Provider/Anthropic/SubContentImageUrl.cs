namespace AIStudio.Provider.Anthropic;

public record SubContentImageUrl : ISubContentImageSource
{
    public SubContentImageType Type => SubContentImageType.URL;

    public string Url { get; init; } = string.Empty;
}