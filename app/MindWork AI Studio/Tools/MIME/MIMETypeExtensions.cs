namespace AIStudio.Tools.MIME;

public static class MIMETypeExtensions
{
    extension(MIMEType[] mimeTypes)
    {
        public string[] ToStringArray()
        {
            var result = new string[mimeTypes.Length];
            for (var i = 0; i < mimeTypes.Length; i++)
            {
                result[i] = mimeTypes[i];
            }
        
            return result;
        }
    }
}
