namespace AIStudio.Tools.MIME;

public class ApplicationBuilder : Builder, ISubtype
{
    private ApplicationBuilder()
    {
    }
    
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
            ApplicationSubtype.EXCEL_OLD => $"{this.baseType}/vnd.ms-excel".ToLowerInvariant(),
            ApplicationSubtype.WORD_OLD => $"{this.baseType}/vnd.ms-word".ToLowerInvariant(),
            ApplicationSubtype.POWERPOINT_OLD => $"{this.baseType}/vnd.ms-powerpoint".ToLowerInvariant(),
            
            ApplicationSubtype.EXCEL => $"{this.baseType}/vnd.openxmlformats-officedocument.spreadsheetml.sheet".ToLowerInvariant(),
            ApplicationSubtype.WORD => $"{this.baseType}/vnd.openxmlformats-officedocument.wordprocessingml.document".ToLowerInvariant(),
            ApplicationSubtype.POWERPOINT => $"{this.baseType}/vnd.openxmlformats-officedocument.presentationml.presentation".ToLowerInvariant(),
            
            _ => $"{this.baseType}/{this.subtype}".ToLowerInvariant()
        }
    };

    #endregion
}