namespace AIStudio.Assistants.EDI;

public static class DataSourcesExtensions
{
    public static string Name(this DataSources dataSource) => dataSource switch
    {
        DataSources.NONE => "No data source selected",
        DataSources.CUSTOM => "Custom description",
        
        DataSources.FILE_SYSTEM => "File system (local or network share)",
        DataSources.OBJECT_STORAGE => "Object storage, like Amazon S3, MinIO, etc.",
        DataSources.KEY_VALUE_STORE => "Key-Value store, like Redis, etc.",
        DataSources.DOCUMENT_STORE => "Document store, like MongoDB, etc.",
        DataSources.RELATIONAL_DATABASE => "Relational database, like MySQL, PostgreSQL, etc.",
        DataSources.GRAPH_DATABASE => "Graph database, like Neo4j, ArangoDB, etc.",
        
        _ => "Unknown data source"
    };
}