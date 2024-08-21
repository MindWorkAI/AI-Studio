namespace AIStudio.Assistants.Coding;

public sealed class CodingContext(string id, CommonCodingLanguages language, string otherLanguage, string code)
{
    public CodingContext() : this(string.Empty, CommonCodingLanguages.NONE, string.Empty, string.Empty)
    {
    }
    
    public string Id { get; set; } = id;
    
    public CommonCodingLanguages Language { get; set; } = language;
    
    public string OtherLanguage { get; set; } = otherLanguage;
    
    public string Code { get; set; } = code;
}