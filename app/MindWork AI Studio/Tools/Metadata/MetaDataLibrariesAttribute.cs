namespace AIStudio.Tools.Metadata;

public class MetaDataLibrariesAttribute(string pdfiumVersion) : Attribute
{
    public string PdfiumVersion => pdfiumVersion;
}