using AIStudio.Assistants.Coding;

namespace AIStudio.Settings.DataModel;

public sealed class DataCoding
{
    /// <summary>
    /// Preselect any coding options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// Preselect the compiler messages?
    /// </summary>
    public bool PreselectCompilerMessages { get; set; }

    /// <summary>
    /// Preselect the coding language for new contexts?
    /// </summary>
    public CommonCodingLanguages PreselectedProgrammingLanguage { get; set; }

    /// <summary>
    /// Do you want to preselect any other language?
    /// </summary>
    public string PreselectedOtherProgrammingLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Which coding provider should be preselected?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
}