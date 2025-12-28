namespace AIStudio.Provider.Anthropic;

public record SubContentBase64Image : ISubContentImageSource
{
    public SubContentImageType Type => SubContentImageType.BASE64;

    public string MediaType { get; init; } = string.Empty;
    
    public string Data { get; init; } = string.Empty;
}