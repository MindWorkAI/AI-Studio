namespace AIStudio.Tools.Metadata;

public class MetaDataDatabasesAttribute(string databaseVersion) : Attribute
{
    public string DatabaseVersion => databaseVersion;
}