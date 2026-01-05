namespace AIStudio.Tools.MIME;

public class ImageBuilder : ISubtype
{
    private readonly BaseType baseType;
    
    private ImageBuilder(BaseType baseType)
    {
        this.baseType = baseType;
    }

    public static ImageBuilder Create() => new(BaseType.IMAGE);

    private ImageSubtype subtype;

    public ImageBuilder UseSubtype(string subType)
    {
        this.subtype = subType.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ImageSubtype.JPEG,
            "png" => ImageSubtype.PNG,
            "gif" => ImageSubtype.GIF,
            "webp" => ImageSubtype.WEBP,
            "tiff" or "tif" => ImageSubtype.TIFF,
            "svg+xml" or "svg" => ImageSubtype.SVG,
            "heic" => ImageSubtype.HEIC,
            
            _ => throw new ArgumentException("Unsupported MIME image subtype.", nameof(subType))
        };

        return this;
    }

    public ImageBuilder UseSubtype(ImageSubtype subType)
    {
        this.subtype = subType;
        return this;
    }
    
    #region Implementation of IMIMESubtype

    public MIMEType Build() => new()
    {
        Type = this,
        TextRepresentation = $"{this.baseType}/{this.subtype}".ToLowerInvariant()
    };

    #endregion
}