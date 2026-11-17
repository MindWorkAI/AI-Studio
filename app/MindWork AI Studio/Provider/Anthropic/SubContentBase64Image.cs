namespace AIStudio.Provider.Anthropic;

public record SubContentBase64Image : ISubContentImageSource
{
    public SubContentImageType Type => SubContentImageType.BASE64;

    public string MediaType { get; set; } = string.Empty;
    
    public string Data { get; set; } = string.Empty;
}