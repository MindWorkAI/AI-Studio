namespace AIStudio.Tools.MIME;

public class ApplicationBuilder : ISubtype
{
    private const BaseType BASE_TYPE = BaseType.APPLICATION;

    private ApplicationBuilder()
    {
    }

    public static ApplicationBuilder Create() => new();
    
    private ApplicationSubtype subtype;
    
    public ApplicationBuilder UseSubtype(string subType)
    {
        this.subtype = subType.ToLowerInvariant() switch
        {
            "vnd.ms-excel" => ApplicationSubtype.EXCEL_OLD,
            "vnd.ms-word" => ApplicationSubtype.WORD_OLD,
            "vnd.ms-powerpoint" => ApplicationSubtype.POWERPOINT_OLD,
            
            "vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ApplicationSubtype.EXCEL,
            "vnd.openxmlformats-officedocument.wordprocessingml.document" => ApplicationSubtype.WORD,
            "vnd.openxmlformats-officedocument.presentationml.presentation" => ApplicationSubtype.POWERPOINT,
            
            "octet-stream" => ApplicationSubtype.OCTET_STREAM,
            
            "json" => ApplicationSubtype.JSON,
            "xml" => ApplicationSubtype.XML,
            "pdf" => ApplicationSubtype.PDF,
            "zip" => ApplicationSubtype.ZIP,
            
            "x-www-form-urlencoded" => ApplicationSubtype.X_WWW_FORM_URLENCODED,
            _ => throw new ArgumentOutOfRangeException(nameof(subType), "Unsupported MIME application subtype.")
        };
        
        return this;
    }
    
    public ApplicationBuilder UseSubtype(ApplicationSubtype subType)
    {
        this.subtype = subType;
        return this;
    }
    
    #region Implementation of IMIMESubtype

    public MIMEType Build() => new()
    {
        Type = this,
        TextRepresentation = this.subtype switch
        {
            ApplicationSubtype.EXCEL_OLD => $"{BASE_TYPE}/vnd.ms-excel".ToLowerInvariant(),
            ApplicationSubtype.WORD_OLD => $"{BASE_TYPE}/vnd.ms-word".ToLowerInvariant(),
            ApplicationSubtype.POWERPOINT_OLD => $"{BASE_TYPE}/vnd.ms-powerpoint".ToLowerInvariant(),
            
            ApplicationSubtype.EXCEL => $"{BASE_TYPE}/vnd.openxmlformats-officedocument.spreadsheetml.sheet".ToLowerInvariant(),
            ApplicationSubtype.WORD => $"{BASE_TYPE}/vnd.openxmlformats-officedocument.wordprocessingml.document".ToLowerInvariant(),
            ApplicationSubtype.POWERPOINT => $"{BASE_TYPE}/vnd.openxmlformats-officedocument.presentationml.presentation".ToLowerInvariant(),
            
            _ => $"{BASE_TYPE}/{this.subtype}".ToLowerInvariant()
        }
    };

    #endregion
}