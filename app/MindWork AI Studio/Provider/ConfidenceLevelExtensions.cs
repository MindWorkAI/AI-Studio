namespace AIStudio.Provider;

public static class ConfidenceLevelExtensions
{
    public static string GetName(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.NONE => "No provider selected",
        
        ConfidenceLevel.UNTRUSTED => "Untrusted",
        ConfidenceLevel.VERY_LOW => "Very Low",
        ConfidenceLevel.LOW => "Low",
        ConfidenceLevel.MODERATE => "Moderate",
        ConfidenceLevel.MEDIUM => "Medium",
        ConfidenceLevel.HIGH => "High",
        
        _ => "Unknown confidence level",
    };
    
    public static string GetColor(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.NONE => "#cccccc",
        
        ConfidenceLevel.UNTRUSTED => "#ff0000",
        ConfidenceLevel.VERY_LOW => "#ff6600",
        ConfidenceLevel.LOW => "#ffcc00",
        ConfidenceLevel.MODERATE => "#99cc00",
        ConfidenceLevel.MEDIUM => "#86b300",
        ConfidenceLevel.HIGH => "#009933",
        
        _ => "#cc6600",
    };
    
    public static string SetColorStyle(this ConfidenceLevel level) => $"--confidence-color: {level.GetColor()};";
}