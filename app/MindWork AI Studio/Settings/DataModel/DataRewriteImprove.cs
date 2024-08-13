using AIStudio.Components.Pages.RewriteImprove;
using AIStudio.Tools;

namespace AIStudio.Settings.DataModel;

public sealed class DataRewriteImprove
{
    /// <summary>
    /// Preselect any rewrite options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }
    
    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect any writing style?
    /// </summary>
    public WritingStyles PreselectedWritingStyle { get; set; }
    
    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}