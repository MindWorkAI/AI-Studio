namespace AIStudio.Settings.DataModel;

public static class PreviewVisibilityExtensions
{
    public static IList<PreviewFeatures> GetPreviewFeatures(this PreviewVisibility visibility)
    {
        var features = new List<PreviewFeatures>();
        if (visibility >= PreviewVisibility.RELEASE_CANDIDATE)
        {
        }
        
        if (visibility >= PreviewVisibility.BETA)
        {
        }
        
        if (visibility >= PreviewVisibility.ALPHA)
        {
        }
        
        if (visibility >= PreviewVisibility.PROTOTYPE)
        {
            features.Add(PreviewFeatures.PRE_RAG_2024);
        }
        
        if (visibility >= PreviewVisibility.EXPERIMENTAL)
        {
            features.Add(PreviewFeatures.PRE_WRITER_MODE_2024);
        }
        
        return features;
    }
    
    public static HashSet<PreviewFeatures> FilterPreviewFeatures(this PreviewVisibility visibility, HashSet<PreviewFeatures> enabledFeatures)
    {
        var filteredFeatures = new HashSet<PreviewFeatures>();
        var previewFeatures = visibility.GetPreviewFeatures();
        foreach (var feature in enabledFeatures)
        {
            if (previewFeatures.Contains(feature))
                filteredFeatures.Add(feature);
        }
        
        return filteredFeatures;
    }
}