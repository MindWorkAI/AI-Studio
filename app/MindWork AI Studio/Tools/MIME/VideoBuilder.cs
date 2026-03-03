namespace AIStudio.Tools.MIME;

public class VideoBuilder : ISubtype
{
    private const BaseType BASE_TYPE = BaseType.VIDEO;

    private VideoBuilder()
    {
    }

    public static VideoBuilder Create() => new();
    
    private VideoSubtype subtype;

    public VideoBuilder UseSubtype(string subType)
    {
        this.subtype = subType.ToLowerInvariant() switch
        {
            "mp4" => VideoSubtype.MP4,
            "webm" => VideoSubtype.WEBM,
            "avi" => VideoSubtype.AVI,
            "mov" => VideoSubtype.MOV,
            "mkv" => VideoSubtype.MKV,
            
            _ => throw new ArgumentException("Unsupported MIME video subtype.", nameof(subType))
        };

        return this;
    }

    public VideoBuilder UseSubtype(VideoSubtype subType)
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