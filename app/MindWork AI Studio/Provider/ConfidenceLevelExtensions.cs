using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Provider;

public static class ConfidenceLevelExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfidenceLevelExtensions).Namespace, nameof(ConfidenceLevelExtensions));
    
    extension(ConfidenceLevel level)
    {
        public string Name => level switch
        {
            ConfidenceLevel.NONE => TB("No provider selected"),
        
            ConfidenceLevel.UNTRUSTED => TB("Untrusted"),
            ConfidenceLevel.UNKNOWN => TB("Unknown"),
            ConfidenceLevel.VERY_LOW => TB("Very Low"),
            ConfidenceLevel.LOW => TB("Low"),
            ConfidenceLevel.MODERATE => TB("Moderate"),
            ConfidenceLevel.MEDIUM => TB("Medium"),
            ConfidenceLevel.HIGH => TB("High"),
        
            _ => TB("Unknown confidence level"),
        };

        /// <summary>
        /// The color of the level for icons, text, and borders on the background of the app.
        /// </summary>
        /// <remarks>
        /// In dark mode, these are light tones: the dark ones of <see cref="GetBackgroundColor"/> almost vanish on the
        /// dark background, especially as the thin strokes of a Lucide icon.
        /// </remarks>
        /// <param name="settingsManager">The settings manager, which knows whether the app shows its dark mode.</param>
        public string GetColor(SettingsManager settingsManager) => (level, settingsManager.IsDarkMode) switch
        {
            (ConfidenceLevel.NONE, false) => "#cccccc",
            (ConfidenceLevel.NONE, true) => "#666666",

            (ConfidenceLevel.UNKNOWN, false) => "#777777",
            (ConfidenceLevel.UNKNOWN, true) => "#aaaaaa",
        
            (ConfidenceLevel.UNTRUSTED, false) => "#ff0000",
            (ConfidenceLevel.UNTRUSTED, true) => "#fe7c75",
        
            (ConfidenceLevel.VERY_LOW, false) => "#ff6600",
            (ConfidenceLevel.VERY_LOW, true) => "#f58b4b",
        
            (ConfidenceLevel.LOW, false) => "#ffcc00",
            (ConfidenceLevel.LOW, true) => "#e3b842",
        
            (ConfidenceLevel.MODERATE, false) => "#99cc00",
            (ConfidenceLevel.MODERATE, true) => "#afc84e",
        
            (ConfidenceLevel.MEDIUM, false) => "#86b300",
            (ConfidenceLevel.MEDIUM, true) => "#8bc55c",
        
            (ConfidenceLevel.HIGH, false) => "#009933",
            (ConfidenceLevel.HIGH, true) => "#5ac576",
        
            (_, false) => "#cc6600",
            (_, true) => "#db9152",
        };

        /// <summary>
        /// The color of the level as the background of white text, such as the levels in the confidence scheme.
        /// </summary>
        /// <remarks>
        /// In light mode, this is the color of <see cref="GetColor"/>. In dark mode, these are dark tones, so the white
        /// text on them stays readable.
        /// </remarks>
        /// <param name="settingsManager">The settings manager, which knows whether the app shows its dark mode.</param>
        public string GetBackgroundColor(SettingsManager settingsManager) => settingsManager.IsDarkMode ? level switch
        {
            ConfidenceLevel.NONE => "#666666",
            ConfidenceLevel.UNKNOWN => "#555555",
            ConfidenceLevel.UNTRUSTED => "#800000",
            ConfidenceLevel.VERY_LOW => "#803300",
            ConfidenceLevel.LOW => "#806600",
            ConfidenceLevel.MODERATE => "#4d6600",
            ConfidenceLevel.MEDIUM => "#394d00",
            ConfidenceLevel.HIGH => "#004d1a",

            _ => "#663300",
        } : level.GetColor(settingsManager);

        public string SetColorStyle(SettingsManager settingsManager) => $"--confidence-color: {level.GetColor(settingsManager)};";
    }
}