namespace AIStudio.Tools.Metadata;

public class MetaDataDatabasesAttribute(string qdrantVersion) : Attribute
{
    public string QdrantVersion => qdrantVersion;
}