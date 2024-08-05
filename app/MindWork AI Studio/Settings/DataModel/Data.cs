namespace AIStudio.Settings.DataModel;

/// <summary>
/// The data model for the settings file.
/// </summary>
public sealed class Data
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; } = Version.V4;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    public DataApp App { get; init; } = new();

    public DataChat Chat { get; init; } = new();

    public DataWorkspace Workspace { get; init; } = new();

    public DataIconFinder IconFinder { get; init; } = new();

    public DataTranslation Translation { get; init; } = new();

    public DataCoding Coding { get; init; } = new();

    public DataTextSummarizer TextSummarizer { get; init; } = new();

    public DataTextContentCleaner TextContentCleaner { get; init; } = new();
}