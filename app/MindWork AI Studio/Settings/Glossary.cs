using AIStudio.Tools.PluginSystem;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// A glossary: a spreadsheet or CSV file whose terms are always translated the same way.
/// </summary>
/// <remarks>
/// Only the path is stored, not the terms. Whoever maintains the glossary keeps working in their spreadsheet,
/// and every translation uses the file as it is at that moment, also when it lies on a shared drive.
/// </remarks>
/// <param name="Num">The number of the glossary.</param>
/// <param name="Id">The unique ID of the glossary.</param>
/// <param name="Name">The name the user sees.</param>
/// <param name="FilePath">The full path of the glossary file.</param>
/// <param name="PreselectInTranslation">Whether the translation assistant uses the glossary until the user deselects it.</param>
/// <param name="IsEnterpriseConfiguration">Whether a configuration plugin provides the glossary.</param>
/// <param name="EnterpriseConfigurationPluginId">The configuration plugin which provides the glossary.</param>
public sealed record Glossary(
    uint Num,
    string Id,
    string Name,
    string FilePath,
    bool PreselectInTranslation,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    public Glossary() : this(0, Guid.Empty.ToString(), string.Empty, string.Empty, false)
    {
    }

    private static readonly ILogger<Glossary> LOGGER = Program.LOGGER_FACTORY.CreateLogger<Glossary>();

    public static readonly Glossary NO_GLOSSARY = new()
    {
        Num = uint.MaxValue,
    };

    #region Overrides of ValueType

    public override string ToString() => this.Name;

    #endregion

    public static bool TryParseGlossaryTable(int idx, LuaTable table, Guid configPluginId, string pluginPath, out ConfigurationBaseObject glossary)
    {
        glossary = NO_GLOSSARY;
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} does not contain a valid ID. The ID must be a valid GUID.", idx);
            return false;
        }

        if (!table.TryGetValue("Name", out var nameValue) || !nameValue.TryRead<string>(out var name) || string.IsNullOrWhiteSpace(name))
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} does not contain a valid name.", idx);
            return false;
        }

        if (!table.TryGetValue("FilePath", out var filePathValue) || !filePathValue.TryRead<string>(out var filePath))
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} does not contain a valid file path.", idx);
            return false;
        }

        //
        // The file itself is not checked here: a shared drive may be offline right now, and the glossary
        // must not vanish from the settings only because of that.
        //
        var pathIssue = PluginFilePaths.TryResolve(filePath, pluginPath, out var resolvedFilePath);
        if (pathIssue is not PluginFilePathIssue.NONE)
        {
            LOGGER.LogWarning("The file path of the configured glossary {IdxGlossary} cannot be used: {PathIssue}.", idx, pathIssue);
            return false;
        }

        var preselectInTranslation = false;
        if (table.TryGetValue("PreselectInTranslation", out var preselectValue) && preselectValue.TryRead<bool>(out var preselect))
            preselectInTranslation = preselect;

        glossary = new Glossary
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            Name = name,
            FilePath = resolvedFilePath,
            PreselectInTranslation = preselectInTranslation,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return true;
    }
}