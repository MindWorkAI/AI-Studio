namespace AIStudio.Tools.MIME;

public class AudioBuilder : Builder, ISubtype
{
    private AudioBuilder()
    {
    }

    private AudioSubtype subtype;

    public AudioBuilder UseSubtype(string subType)
    {
        this.subtype = subType.ToLowerInvariant() switch
        {
            "mpeg" => AudioSubtype.MPEG,
            "wav" => AudioSubtype.WAV,
            "ogg" => AudioSubtype.OGG,
            "aac" => AudioSubtype.AAC,
            "flac" => AudioSubtype.FLAC,
            "webm" => AudioSubtype.WEBM,
            "mp4" => AudioSubtype.MP4,
            "mp3" => AudioSubtype.MP3,
            "m4a" => AudioSubtype.M4A,
            "aiff" => AudioSubtype.AIFF,
            
            _ => throw new ArgumentException("Unsupported MIME audio subtype.", nameof(subType))
        };
        
        return this;
    }

    public AudioBuilder UseSubtype(AudioSubtype subType)
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