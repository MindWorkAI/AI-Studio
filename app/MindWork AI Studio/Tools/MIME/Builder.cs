namespace AIStudio.Tools.MIME;

public class Builder
{
    protected Builder()
    {
    }

    protected BaseType baseType;
    
    public static Builder Create() => new();
    
    public static MIMEType FromTextRepresentation(string textRepresentation)
    {
        var parts = textRepresentation.Split('/');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid MIME type format.", nameof(textRepresentation));
        
        var baseType = parts[0].ToLowerInvariant();
        var subType = parts[1].ToLowerInvariant();
        
        var builder = Create();

        switch (baseType)
        {
            case "application":
                var appBuilder = builder.UseApplication();
                return appBuilder.UseSubtype(subType).Build();

            case "text":
                var textBuilder = builder.UseText();
                return textBuilder.UseSubtype(subType).Build();
            
            case "audio":
                var audioBuilder = builder.UseAudio();
                return audioBuilder.UseSubtype(subType).Build();
            
            case "image":
                var imageBuilder = builder.UseImage();
                return imageBuilder.UseSubtype(subType).Build();
            
            case "video":
                var videoBuilder = builder.UseVideo();
                return videoBuilder.UseSubtype(subType).Build();
            
            default:
                throw new ArgumentException("Unsupported base type.", nameof(textRepresentation));
        }
    }
    
    public ApplicationBuilder UseApplication() 
    {
        this.baseType = BaseType.APPLICATION;
        return (ApplicationBuilder)this;
    }
    
    public TextBuilder UseText() 
    {
        this.baseType = BaseType.TEXT;
        return (TextBuilder)this;
    }
    
    public AudioBuilder UseAudio() 
    {
        this.baseType = BaseType.AUDIO;
        return (AudioBuilder)this;
    }
    
    public ImageBuilder UseImage() 
    {
        this.baseType = BaseType.IMAGE;
        return (ImageBuilder)this;
    }
    
    public VideoBuilder UseVideo() 
    {
        this.baseType = BaseType.VIDEO;
        return (VideoBuilder)this;
    }
}