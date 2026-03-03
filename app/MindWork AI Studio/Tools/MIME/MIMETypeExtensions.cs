namespace AIStudio.Tools.MIME;

public static class MIMETypeExtensions
{
    public static string[] ToStringArray(this MIMEType[] mimeTypes)
    {
        var result = new string[mimeTypes.Length];
        for (var i = 0; i < mimeTypes.Length; i++)
        {
            result[i] = mimeTypes[i];
        }
        
        return result;
    }
}
