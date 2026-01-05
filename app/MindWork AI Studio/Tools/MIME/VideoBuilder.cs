namespace AIStudio.Tools.MIME;

public class VideoBuilder : Builder, ISubtype
{
    private VideoBuilder()
    {
    }
    
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
        TextRepresentation = $"{this.baseType}/{this.subtype}".ToLowerInvariant()
    };

    #endregion
}