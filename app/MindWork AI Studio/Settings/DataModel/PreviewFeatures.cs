namespace AIStudio.Settings.DataModel;

public enum PreviewFeatures
{
    NONE = 0,
    
    //
    // Important: Never delete any enum value from this list.
    // We must be able to deserialize old settings files that may contain these values.
    //
    PRE_WRITER_MODE_2024,
    PRE_RAG_2024,
    
    PRE_PLUGINS_2025,
    PRE_READ_PDF_2025,
    PRE_DOCUMENT_ANALYSIS_2025,
    PRE_SPEECH_TO_TEXT_2026,
}