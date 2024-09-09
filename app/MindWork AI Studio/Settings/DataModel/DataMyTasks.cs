namespace AIStudio.Settings.DataModel;

public sealed class DataMyTasks
{
    /// <summary>
    /// Do you want to preselect any options?
    /// </summary>
    public bool PreselectOptions { get; set; }
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }
    
    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The preselected provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
}