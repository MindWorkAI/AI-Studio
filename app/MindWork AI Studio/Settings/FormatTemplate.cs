using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

using SharedTools;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// A template for the documents AI Studio writes, e.g., a Word or a PowerPoint template of an organization.
/// </summary>
/// <remarks>
/// The file stays where the user keeps it, so that a new version of the template takes effect at
/// once. Hence, FilePath is the absolute path of a file of the user, not one below the data directory.
/// Which documents a template serves follows from the type of its file, cf. FileTypes.FORMAT_TEMPLATE:
/// a Word template serves the export to Word, a PowerPoint template serves presentations.
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

    /// <summary>
    /// Reads a format template the way a configuration plugin states it.
    /// </summary>
    /// <remarks>
    /// A relative path names a file the plugin ships, which has to stay inside the plugin directory.
    /// An absolute path, e.g., on a network share, stays as the organization wrote it and is not
    /// looked up here: an import reads a pasted snippet through this method as well, and it must not
    /// reach out to a share before the user agreed.
    /// </remarks>
    /// <param name="idx">The number of the format template in its Lua table, for the log.</param>
    /// <param name="table">The Lua table of the format template.</param>
    /// <param name="configPluginId">The ID of the configuration plugin which states the format template.</param>
    /// <param name="pluginPath">The directory of that plugin, against which a relative path is resolved.</param>
    /// <param name="formatTemplate">The format template, when it could be read.</param>
    /// <returns>True when the format template could be read.</returns>
    public static bool TryParseFormatTemplateTable(int idx, LuaTable table, Guid configPluginId, string pluginPath, out ConfigurationBaseObject formatTemplate)
    {
        formatTemplate = new FormatTemplate();
        var logger = Program.LOGGER_FACTORY.CreateLogger<FormatTemplate>();
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            logger.LogWarning("The configured format template {IdxFormatTemplate} does not contain a valid ID. The ID must be a valid GUID.", idx);
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
        {
            logger.LogWarning("The configured format template {IdxFormatTemplate} does not contain a valid name.", idx);
            return false;
        }

        if (!table.TryGetValue("FilePath", out var filePathValue) || !filePathValue.TryRead<string>(out var filePath) || string.IsNullOrWhiteSpace(filePath))
        {
            logger.LogWarning("The configured format template {IdxFormatTemplate} does not contain a file path.", idx);
            return false;
        }

        if (!PluginFilePath.TryResolve(filePath, pluginPath, out var resolvedFilePath, out var issue))
        {
            logger.LogWarning("The file path of the configured format template {IdxFormatTemplate} cannot be used: {Issue}", idx, issue);
            return false;
        }

        if (!FileTypes.IsAllowedPath(resolvedFilePath, FileTypes.FORMAT_TEMPLATE))
        {
            logger.LogWarning("The file of the configured format template {IdxFormatTemplate} is not a supported format template: {FilePath}", idx, resolvedFilePath);
            return false;
        }

        formatTemplate = new FormatTemplate
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            Name = name,
            FilePath = resolvedFilePath,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return true;
    }

    /// <summary>
    /// Exports the format template as a Lua configuration section.
    /// </summary>
    /// <remarks>
    /// The path stays as it is, so it has to be readable on every machine which reads the
    /// configuration, e.g., on a network share. Otherwise, the organization ships the file with
    /// its configuration plugin and states its path relative to the plugin.
    /// </remarks>
    /// <returns>A Lua configuration section string.</returns>
    public string ExportAsConfigurationSection()
    {
        return $$"""
                CONFIG["FORMAT_TEMPLATES"][#CONFIG["FORMAT_TEMPLATES"]+1] = {
                    ["Id"] = "{{Guid.NewGuid().ToString()}}",
                    ["Name"] = {{LuaTools.ToLuaStringLiteral(this.Name)}},
                    ["FilePath"] = {{LuaTools.ToLuaStringLiteral(this.FilePath)}},
                }
                """;
    }
}