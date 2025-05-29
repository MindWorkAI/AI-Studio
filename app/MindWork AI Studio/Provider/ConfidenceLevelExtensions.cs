using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

public static class ConfidenceLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfidenceLevelExtensions).Namespace, nameof(ConfidenceLevelExtensions));
    
    public static string GetName(this ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.NONE => TB("No provider selected"),
        
        ConfidenceLevel.UNTRUSTED => TB("Untrusted"),
        ConfidenceLevel.VERY_LOW => TB("Very Low"),
        ConfidenceLevel.LOW => TB("Low"),
        ConfidenceLevel.MODERATE => TB("Moderate"),
        ConfidenceLevel.MEDIUM => TB("Medium"),
        ConfidenceLevel.HIGH => TB("High"),
        
        _ => TB("Unknown confidence level"),
    };
    
    public static string GetColor(this ConfidenceLevel level, SettingsManager settingsManager) => (level, settingsManager.IsDarkMode) switch
    {
        (ConfidenceLevel.NONE, _) => "#cccccc",
        
        (ConfidenceLevel.UNTRUSTED, false) => "#ff0000",
        (ConfidenceLevel.UNTRUSTED, true) => "#800000",
        
        (ConfidenceLevel.VERY_LOW, false) => "#ff6600",
        (ConfidenceLevel.VERY_LOW, true) => "#803300",
        
        (ConfidenceLevel.LOW, false) => "#ffcc00",
        (ConfidenceLevel.LOW, true) => "#806600",
        
        (ConfidenceLevel.MODERATE, false) => "#99cc00",
        (ConfidenceLevel.MODERATE, true) => "#4d6600",
        
        (ConfidenceLevel.MEDIUM, false) => "#86b300",
        (ConfidenceLevel.MEDIUM, true) => "#394d00",
        
        (ConfidenceLevel.HIGH, false) => "#009933",
        (ConfidenceLevel.HIGH, true) => "#004d1a",
        
        (_, false) => "#cc6600",
        (_, true) => "#663300",
    };
    
    public static string SetColorStyle(this ConfidenceLevel level, SettingsManager settingsManager) => $"--confidence-color: {level.GetColor(settingsManager)};";
}