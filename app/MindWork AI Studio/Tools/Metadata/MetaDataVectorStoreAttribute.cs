namespace AIStudio.Tools.Metadata;

public class MetaDataVectorStoreAttribute(string vectorStoreVersion) : Attribute
{
    public string VectorStoreVersion => vectorStoreVersion;
}