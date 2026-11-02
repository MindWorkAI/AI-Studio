using System.Text;
using System.Text.Json.Serialization;

using AIStudio.Tools;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.PluginSystem;

using SharedTools;

using LuaTable = Lua.LuaTable;

namespace AIStudio.Settings;

/// <summary>
/// A glossary: terms with a fixed translation or an explanation, which AI Studio hands to the model.
/// </summary>
/// <remarks>
/// The settings hold only what describes the glossary. Its entries can run into the thousands, so they
/// live in a file of their own, which the GlossaryStore derives from the ID. A spreadsheet or CSV file
/// serves only to import entries once; afterward, AI Studio keeps them itself.
/// </remarks>
/// <param name="Num">The number of the glossary.</param>
/// <param name="Id">The unique ID of the glossary.</param>
/// <param name="Name">The name the user sees.</param>
/// <param name="Type">What the values of the glossary mean.</param>
/// <param name="Description">What the glossary is about and when it applies, for the user and the model.</param>
/// <param name="SourceLanguage">The language of the terms; only a translation glossary needs it.</param>
/// <param name="CustomSourceLanguage">The language of the terms when the source language is OTHER.</param>
/// <param name="TargetLanguage">The language of the values; only a translation glossary needs it.</param>
/// <param name="CustomTargetLanguage">The language of the values when the target language is OTHER.</param>
/// <param name="PreselectInTranslation">Whether the translation assistant uses the glossary until the user deselects it, provided it suits the target language.</param>
/// <param name="IsEnterpriseConfiguration">Whether a configuration plugin provides the glossary.</param>
/// <param name="EnterpriseConfigurationPluginId">The configuration plugin which provides the glossary.</param>
public sealed record Glossary(
    uint Num,
    string Id,
    string Name,
    GlossaryType Type,
    string Description,
    CommonLanguages SourceLanguage,
    string CustomSourceLanguage,
    CommonLanguages TargetLanguage,
    string CustomTargetLanguage,
    bool PreselectInTranslation,
    bool IsEnterpriseConfiguration = false,
    Guid EnterpriseConfigurationPluginId = default) : ConfigurationBaseObject
{
    //
    // A new glossary is meant to be used: whoever adds a glossary into English expects the next
    // translation into English to follow it.
    //
    public Glossary() : this(0, Guid.Empty.ToString(), string.Empty, GlossaryType.TRANSLATION, string.Empty, CommonLanguages.AS_IS, string.Empty, CommonLanguages.AS_IS, string.Empty, PreselectInTranslation: true)
    {
    }

    private static readonly ILogger<Glossary> LOGGER = Program.LOGGER_FACTORY.CreateLogger<Glossary>();

    public static readonly Glossary NO_GLOSSARY = new()
    {
        Num = uint.MaxValue,
    };

    /// <summary>
    /// Where the entries come from, for a glossary just read from a configuration plugin.
    /// </summary>
    /// <remarks>
    /// Never stored: the plugin brings its entries again whenever it loads, cf. GlossaryStore.
    /// </remarks>
    [JsonIgnore]
    public GlossaryPluginSource? PluginSource { get; init; }

    #region Overrides of ValueType

    public override string ToString() => this.Name;

    #endregion

    /// <summary>
    /// Exports the glossary as a Lua configuration section, together with its entries.
    /// </summary>
    /// <remarks>
    /// The entries are listed in the section itself rather than named as a file: whoever receives the
    /// section, a colleague or a configuration plugin, has no access to the files of this computer.
    /// The section gets an ID of its own, so importing it never replaces the glossary it came from.
    /// </remarks>
    /// <param name="entries">The entries of the glossary.</param>
    /// <returns>A Lua configuration section string.</returns>
    public string ExportAsConfigurationSection(IReadOnlyList<GlossaryEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""CONFIG["GLOSSARIES"][#CONFIG["GLOSSARIES"]+1] = {""");
        builder.AppendLine($"""    ["Id"] = "{Guid.NewGuid()}",""");
        builder.AppendLine($"""    ["Name"] = {LuaTools.ToLuaStringLiteral(this.Name)},""");
        builder.AppendLine($"""    ["Type"] = "{this.Type}",""");
        builder.AppendLine($"""    ["Description"] = {LuaTools.ToLuaStringLiteral(this.Description)},""");
        if (this.Type is GlossaryType.TRANSLATION)
        {
            builder.AppendLine($"""    ["SourceLanguage"] = "{this.SourceLanguage}",""");
            builder.AppendLine($"""    ["CustomSourceLanguage"] = {LuaTools.ToLuaStringLiteral(this.CustomSourceLanguage)},""");
            builder.AppendLine($"""    ["TargetLanguage"] = "{this.TargetLanguage}",""");
            builder.AppendLine($"""    ["CustomTargetLanguage"] = {LuaTools.ToLuaStringLiteral(this.CustomTargetLanguage)},""");
        }

        builder.AppendLine($"""    ["PreselectInTranslation"] = {this.PreselectInTranslation.ToString().ToLowerInvariant()},""");
        builder.AppendLine("""    ["Entries"] = {""");
        foreach (var entry in entries)
        {
            builder.Append($$"""        { ["Term"] = {{LuaTools.ToLuaStringLiteral(entry.Term)}}, ["Value"] = {{LuaTools.ToLuaStringLiteral(entry.Value)}}""");
            if (entry.Aliases is { Count: > 0 } aliases)
                builder.Append($$""", ["OtherForms"] = { {{string.Join(", ", aliases.Select(alias => LuaTools.ToLuaStringLiteral(alias)))}} }""");

            if (!string.IsNullOrWhiteSpace(entry.Note))
                builder.Append($$""", ["Note"] = {{LuaTools.ToLuaStringLiteral(entry.Note)}}""");

            builder.AppendLine(" },");
        }

        builder.AppendLine("    },");
        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>
    /// Reads a glossary from the Lua table of a configuration plugin.
    /// </summary>
    /// <param name="idx">The position of the glossary in the plugin, for the log.</param>
    /// <param name="table">The Lua table of the glossary.</param>
    /// <param name="configPluginId">The configuration plugin which provides the glossary.</param>
    /// <param name="pluginPath">The folder of the plugin, which the path of a glossary file is relative to.</param>
    /// <param name="glossary">The glossary, with its entries as <see cref="PluginSource"/>.</param>
    /// <returns>True when the table describes a valid glossary.</returns>
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

        if (!TryReadEnum<GlossaryType>(table, "Type", out var type))
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} does not contain a valid type. Valid types are TRANSLATION and REFERENCE.", idx);
            return false;
        }

        var sourceLanguage = CommonLanguages.AS_IS;
        var customSourceLanguage = string.Empty;
        var targetLanguage = CommonLanguages.AS_IS;
        var customTargetLanguage = string.Empty;
        if (type is GlossaryType.TRANSLATION)
        {
            if (!TryReadLanguage(idx, table, "SourceLanguage", "CustomSourceLanguage", out sourceLanguage, out customSourceLanguage)
                || !TryReadLanguage(idx, table, "TargetLanguage", "CustomTargetLanguage", out targetLanguage, out customTargetLanguage))
                return false;
        }

        var description = string.Empty;
        if (table.TryGetValue("Description", out var descriptionValue) && descriptionValue.TryRead<string>(out var descriptionText))
            description = descriptionText.Trim();

        // Like a glossary the user adds, one of the organization is meant to be used:
        var preselectInTranslation = true;
        if (table.TryGetValue("PreselectInTranslation", out var preselectValue) && preselectValue.TryRead<bool>(out var preselect))
            preselectInTranslation = preselect;

        //
        // A file is read only once the plugin has started, cf. PluginConfigurationObject.SyncManagedGlossariesAsync.
        // Its existence is not checked here either: the import preview reads the plugin as well, and
        // a glossary it does not list would surprise the organization later.
        //
        GlossaryPluginFile? file = null;
        if (table.TryGetValue("File", out var fileValue) && !fileValue.TryRead<LuaTable>(out _))
        {
            LOGGER.LogWarning("The 'File' of the configured glossary {IdxGlossary} is not a table.", idx);
            return false;
        }

        if (fileValue.TryRead<LuaTable>(out var fileTable) && !TryParseFile(idx, fileTable, pluginPath, out file))
            return false;

        var entries = ParseEntries(idx, table);
        if (file is not null && entries.Count > 0)
            LOGGER.LogWarning("The configured glossary {IdxGlossary} names a file and lists entries. The entries of the file count; the listed ones are ignored.", idx);
        else if (file is null && entries.Count == 0)
            LOGGER.LogWarning("The configured glossary {IdxGlossary} has no entries.", idx);

        glossary = new Glossary
        {
            Num = 0, // will be set later by the PluginConfigurationObject
            Id = id.ToString(),
            Name = name.Trim(),
            Type = type,
            Description = description,
            SourceLanguage = sourceLanguage,
            CustomSourceLanguage = customSourceLanguage,
            TargetLanguage = targetLanguage,
            CustomTargetLanguage = customTargetLanguage,
            PreselectInTranslation = preselectInTranslation,
            IsEnterpriseConfiguration = true,
            EnterpriseConfigurationPluginId = configPluginId,
            PluginSource = file is null ? new GlossaryPluginSource(entries) : new GlossaryPluginSource([], file),
        };

        return true;
    }

    /// <summary>
    /// Reads the file which holds the entries: its path within the plugin folder and the headers of its columns.
    /// </summary>
    /// <remarks>
    /// Only a relative path within the plugin folder is accepted. The file comes with the plugin, so it
    /// is always there, and a plugin cannot make AI Studio read any other file of the computer.
    /// </remarks>
    private static bool TryParseFile(int idx, LuaTable fileTable, string pluginPath, out GlossaryPluginFile? file)
    {
        file = null;
        if (!fileTable.TryGetValue("Path", out var pathValue) || !pathValue.TryRead<string>(out var path) || string.IsNullOrWhiteSpace(path))
        {
            LOGGER.LogWarning("The file of the configured glossary {IdxGlossary} has no 'Path'.", idx);
            return false;
        }

        if (!TryResolvePluginFile(path, pluginPath, out var fullPath))
        {
            LOGGER.LogWarning("The file '{Path}' of the configured glossary {IdxGlossary} is not a relative path within the plugin folder.", path, idx);
            return false;
        }

        if (!TryReadColumn(fileTable, "TermColumn", out var termColumn) || !TryReadColumn(fileTable, "ValueColumn", out var valueColumn))
        {
            LOGGER.LogWarning("The file of the configured glossary {IdxGlossary} has to name its 'TermColumn' and its 'ValueColumn'.", idx);
            return false;
        }

        TryReadColumn(fileTable, "OtherFormsColumn", out var otherFormsColumn);
        TryReadColumn(fileTable, "NoteColumn", out var noteColumn);
        file = new GlossaryPluginFile(fullPath, termColumn!, valueColumn!, otherFormsColumn, noteColumn);
        return true;
    }

    private static bool TryReadColumn(LuaTable fileTable, string field, out string? column)
    {
        column = null;
        if (!fileTable.TryGetValue(field, out var columnValue) || !columnValue.TryRead<string>(out var columnText) || string.IsNullOrWhiteSpace(columnText))
            return false;

        column = columnText.Trim();
        return true;
    }

    /// <summary>
    /// Resolves a path within the plugin folder, refusing every path which leaves it.
    /// </summary>
    /// <param name="path">The path as the plugin states it, relative to its folder.</param>
    /// <param name="pluginPath">The folder of the plugin.</param>
    /// <param name="fullPath">The full path of the file.</param>
    /// <returns>True when the path is relative and stays within the plugin folder.</returns>
    internal static bool TryResolvePluginFile(string path, string pluginPath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(pluginPath) || Path.IsPathRooted(path))
            return false;

        var relativePath = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            return false;

        var pluginRoot = Path.GetFullPath(pluginPath);
        var combinedPath = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        if (!PathTools.IsInsideDirectory(pluginRoot, combinedPath))
            return false;

        fullPath = combinedPath;
        return true;
    }

    private static bool TryReadLanguage(int idx, LuaTable table, string languageField, string customLanguageField, out CommonLanguages language, out string customLanguage)
    {
        customLanguage = string.Empty;
        if (!TryReadEnum(table, languageField, out language) || language is CommonLanguages.AS_IS)
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} does not contain a valid '{LanguageField}'. A translation glossary needs both of its languages, e.g. DE_DE or EN_US.", idx, languageField);
            return false;
        }

        if (language is not CommonLanguages.OTHER)
            return true;

        if (!table.TryGetValue(customLanguageField, out var customValue) || !customValue.TryRead<string>(out var customText) || string.IsNullOrWhiteSpace(customText))
        {
            LOGGER.LogWarning("The configured glossary {IdxGlossary} uses the language OTHER but does not name it in '{CustomLanguageField}'.", idx, customLanguageField);
            return false;
        }

        customLanguage = customText.Trim();
        return true;
    }

    //
    // Only the names count. Enum.TryParse would also take a number, which names nothing to whoever
    // reads the plugin:
    //
    private static bool TryReadEnum<TEnum>(LuaTable table, string field, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        return table.TryGetValue(field, out var luaValue)
               && luaValue.TryRead<string>(out var text)
               && !int.TryParse(text, out _)
               && Enum.TryParse(text.Trim(), true, out value)
               && Enum.IsDefined(value);
    }

    /// <summary>
    /// Reads the entries the plugin lists in the table "Entries"; entries without a term or a value are left out.
    /// </summary>
    private static List<GlossaryEntry> ParseEntries(int idx, LuaTable table)
    {
        var entries = new List<GlossaryEntry>();
        if (!table.TryGetValue("Entries", out var entriesValue) || !entriesValue.TryRead<LuaTable>(out var entriesTable))
            return entries;

        for (var i = 1; i <= entriesTable.ArrayLength; i++)
        {
            if (!entriesTable[i].TryRead<LuaTable>(out var entryTable)
                || !entryTable.TryGetValue("Term", out var termValue) || !termValue.TryRead<string>(out var term) || string.IsNullOrWhiteSpace(term)
                || !entryTable.TryGetValue("Value", out var valueValue) || !valueValue.TryRead<string>(out var value) || string.IsNullOrWhiteSpace(value))
            {
                LOGGER.LogWarning("The entry {IdxEntry} of the configured glossary {IdxGlossary} has no term or no value and is left out.", i, idx);
                continue;
            }

            var note = string.Empty;
            if (entryTable.TryGetValue("Note", out var noteValue) && noteValue.TryRead<string>(out var noteText))
                note = noteText.Trim();

            var otherForms = new List<string>();
            if (entryTable.TryGetValue("OtherForms", out var otherFormsValue) && otherFormsValue.TryRead<LuaTable>(out var otherFormsTable))
            {
                for (var j = 1; j <= otherFormsTable.ArrayLength; j++)
                    if (otherFormsTable[j].TryRead<string>(out var otherForm) && !string.IsNullOrWhiteSpace(otherForm))
                        otherForms.Add(otherForm.Trim());
            }

            entries.Add(new GlossaryEntry(term.Trim(), value.Trim(), otherForms.Count > 0 ? otherForms : null, note));
        }

        return entries;
    }
}