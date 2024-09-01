using AIStudio.Assistants.EMail;

namespace AIStudio.Settings.DataModel;

public sealed class DataEMail
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
    public string PreselectOtherLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Preselect any writing style?
    /// </summary>
    public WritingStyles PreselectedWritingStyle { get; set; }
    
    /// <summary>
    /// Preselect a provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Preselect a greeting phrase?
    /// </summary>
    public string Greeting { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the sender name for the closing salutation?
    /// </summary>
    public string SenderName { get; set; } = string.Empty;
}