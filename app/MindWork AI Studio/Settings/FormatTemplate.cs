using AIStudio.Tools.PluginSystem;

namespace AIStudio.Settings;

/// <summary>
/// A template into which content is exported as a document, e.g., a Word template of an organization.
/// </summary>
/// <remarks>
/// The file stays where the user keeps it, so that a new version of the template takes effect at
/// once. Hence, FilePath is the absolute path of a file of the user, not one below the data directory.
/// Which export format a template serves follows from the type of its file, cf. FileTypes.FORMAT_TEMPLATE.
/// </remarks>
public record FormatTemplate(
    uint Num,
    string Id,
    string Name,
    string FilePath,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    public FormatTemplate() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Returns the name of the format template.
    /// </summary>
    /// <returns>The name of the format template.</returns>
    public override string ToString() => this.Name;
}