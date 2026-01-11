namespace AIStudio.Tools.MIME;

public class Builder
{
    private Builder()
    {
    }
    
    public static Builder Create() => new();
    
    public static MIMEType FromFilename(string filenameOrPath)
    {
        var extension = Path.GetExtension(filenameOrPath);
        if (string.IsNullOrEmpty(extension))
            throw new ArgumentException("Filename or path does not have a valid extension.", nameof(filenameOrPath));

        extension = extension.TrimStart('.').ToLowerInvariant();

        var builder = Create();
        return extension switch
        {
            // Application types
            "pdf" => builder.UseApplication().UseSubtype(ApplicationSubtype.PDF).Build(),
            "zip" => builder.UseApplication().UseSubtype(ApplicationSubtype.ZIP).Build(),
            "doc" => builder.UseApplication().UseSubtype(ApplicationSubtype.WORD_OLD).Build(),
            "docx" => builder.UseApplication().UseSubtype(ApplicationSubtype.WORD).Build(),
            "xls" => builder.UseApplication().UseSubtype(ApplicationSubtype.EXCEL_OLD).Build(),
            "xlsx" => builder.UseApplication().UseSubtype(ApplicationSubtype.EXCEL).Build(),
            "ppt" => builder.UseApplication().UseSubtype(ApplicationSubtype.POWERPOINT_OLD).Build(),
            "pptx" => builder.UseApplication().UseSubtype(ApplicationSubtype.POWERPOINT).Build(),
            "json" => builder.UseApplication().UseSubtype(ApplicationSubtype.JSON).Build(),
            "xml" => builder.UseApplication().UseSubtype(ApplicationSubtype.XML).Build(),

            // Text types
            "txt" => builder.UseText().UseSubtype(TextSubtype.PLAIN).Build(),
            "html" or "htm" => builder.UseText().UseSubtype(TextSubtype.HTML).Build(),
            "css" => builder.UseText().UseSubtype(TextSubtype.CSS).Build(),
            "csv" => builder.UseText().UseSubtype(TextSubtype.CSV).Build(),
            "js" => builder.UseText().UseSubtype(TextSubtype.JAVASCRIPT).Build(),
            "md" or "markdown" => builder.UseText().UseSubtype(TextSubtype.MARKDOWN).Build(),

            // Audio types
            "wav" => builder.UseAudio().UseSubtype(AudioSubtype.WAV).Build(),
            "mp3" => builder.UseAudio().UseSubtype(AudioSubtype.MP3).Build(),
            "ogg" => builder.UseAudio().UseSubtype(AudioSubtype.OGG).Build(),
            "aac" => builder.UseAudio().UseSubtype(AudioSubtype.AAC).Build(),
            "flac" => builder.UseAudio().UseSubtype(AudioSubtype.FLAC).Build(),
            "m4a" => builder.UseAudio().UseSubtype(AudioSubtype.M4A).Build(),
            "aiff" or "aif" => builder.UseAudio().UseSubtype(AudioSubtype.AIFF).Build(),
            "mpga" => builder.UseAudio().UseSubtype(AudioSubtype.MPEG).Build(),
            "webm" => builder.UseAudio().UseSubtype(AudioSubtype.WEBM).Build(),

            // Image types
            "jpg" or "jpeg" => builder.UseImage().UseSubtype(ImageSubtype.JPEG).Build(),
            "png" => builder.UseImage().UseSubtype(ImageSubtype.PNG).Build(),
            "gif" => builder.UseImage().UseSubtype(ImageSubtype.GIF).Build(),
            "tiff" or "tif" => builder.UseImage().UseSubtype(ImageSubtype.TIFF).Build(),
            "webp" => builder.UseImage().UseSubtype(ImageSubtype.WEBP).Build(),
            "svg" => builder.UseImage().UseSubtype(ImageSubtype.SVG).Build(),
            "heic" => builder.UseImage().UseSubtype(ImageSubtype.HEIC).Build(),

            // Video types
            "mp4" => builder.UseVideo().UseSubtype(VideoSubtype.MP4).Build(),
            "avi" => builder.UseVideo().UseSubtype(VideoSubtype.AVI).Build(),
            "mov" => builder.UseVideo().UseSubtype(VideoSubtype.MOV).Build(),
            "mkv" => builder.UseVideo().UseSubtype(VideoSubtype.MKV).Build(),
            "mpeg" or "mpg" => builder.UseVideo().UseSubtype(VideoSubtype.MPEG).Build(),

            _ => throw new ArgumentException($"Unsupported file extension: '.{extension}'.", nameof(filenameOrPath))
        };
    }
    
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
    
    public ApplicationBuilder UseApplication() => ApplicationBuilder.Create();

    public TextBuilder UseText() => TextBuilder.Create();

    public AudioBuilder UseAudio() => AudioBuilder.Create();

    public ImageBuilder UseImage() => ImageBuilder.Create();

    public VideoBuilder UseVideo() => VideoBuilder.Create();
}