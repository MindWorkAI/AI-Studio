namespace AIStudio.Tools.MIME;

public class ImageBuilder : ISubtype
{
    private const BaseType BASE_TYPE = BaseType.IMAGE;

    private ImageBuilder()
    {
    }

    public static ImageBuilder Create() => new();

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
        TextRepresentation = $"{BASE_TYPE}/{this.subtype}".ToLowerInvariant()
    };

    #endregion
}