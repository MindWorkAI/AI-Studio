namespace AIStudio.Tools.MIME;

public class TextBuilder : Builder, ISubtype
{
    private TextBuilder()
    {
    }
    
    private TextSubtype subtype;

    public TextBuilder UseSubtype(string subType)
    {
        this.subtype = subType.ToLowerInvariant() switch
        {
            "plain" => TextSubtype.PLAIN,
            "html" => TextSubtype.HTML,
            "css" => TextSubtype.CSS,
            "csv" => TextSubtype.CSV,
            "javascript" => TextSubtype.JAVASCRIPT,
            "xml" => TextSubtype.XML,
            "markdown" => TextSubtype.MARKDOWN,
            "json" => TextSubtype.JSON,
            
            _ => throw new ArgumentException("Unsupported MIME text subtype.", nameof(subType))
        };
        
        return this;
    }

    public TextBuilder UseSubtype(TextSubtype subType)
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