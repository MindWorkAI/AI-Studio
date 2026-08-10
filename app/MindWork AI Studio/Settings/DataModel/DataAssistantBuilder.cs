namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the default options for the Assistant Builder.
/// </summary>
public sealed class DataAssistantBuilder
{
    /// <summary>
    /// Gets or sets whether Assistant Builder defaults are preselected.
    /// </summary>
    public bool PreselectOptions { get; set; } = true;

    /// <summary>
    /// Gets or sets the provider used by default. An empty value uses the app default.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default output language for generated assistants.
    /// </summary>
    public CommonLanguages PreselectedOutputLanguage { get; set; } = CommonLanguages.AS_IS;

    /// <summary>
    /// Gets or sets the default custom output language.
    /// </summary>
    public string PreselectedOtherOutputLanguage { get; set; } = string.Empty;

}
