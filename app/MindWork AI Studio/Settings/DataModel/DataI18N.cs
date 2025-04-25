namespace AIStudio.Settings.DataModel;

public class DataI18N
{
    /// <summary>
    /// Preselect any I18N options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect a language plugin to where the new content should compare to?
    /// </summary>
    public Guid PreselectedLanguagePluginId { get; set; }

    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; } = CommonLanguages.EN_GB;

    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// Which LLM provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}