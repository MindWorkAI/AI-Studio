namespace AIStudio.Assistants.ERI;

public static class DataSourcesExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(DataSourcesExtensions).Namespace, nameof(DataSourcesExtensions));
    
    public static string Name(this DataSources dataSource) => dataSource switch
    {
        DataSources.NONE => TB("No data source selected"),
        DataSources.CUSTOM => TB("Custom description"),
        
        DataSources.FILE_SYSTEM => TB("File system (local or network share)"),
        DataSources.OBJECT_STORAGE => TB("Object storage, like Amazon S3, MinIO, etc."),
        DataSources.KEY_VALUE_STORE => TB("Key-Value store, like Redis, etc."),
        DataSources.DOCUMENT_STORE => TB("Document store, like MongoDB, etc."),
        DataSources.RELATIONAL_DATABASE => TB("Relational database, like MySQL, PostgreSQL, etc."),
        DataSources.GRAPH_DATABASE => TB("Graph database, like Neo4j, ArangoDB, etc."),
        
        _ => TB("Unknown data source")
    };
}