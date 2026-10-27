using System.Diagnostics.CodeAnalysis;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Turns the rows of a table into glossary entries and combines them with the entries a glossary holds already.
/// </summary>
public static class GlossaryImport
{
    private const char ALIAS_SEPARATOR = ';';

    //
    // Header rows name a language in many ways. Besides the IETF tags, these are the names and codes
    // seen most often in glossaries, in English and in German:
    //
    private static readonly Dictionary<string, CommonLanguages> LANGUAGE_NAMES = new(StringComparer.Ordinal)
    {
        ["de"] = CommonLanguages.DE_DE, ["german"] = CommonLanguages.DE_DE, ["deutsch"] = CommonLanguages.DE_DE,
        ["en"] = CommonLanguages.EN_US, ["english"] = CommonLanguages.EN_US, ["englisch"] = CommonLanguages.EN_US,
        ["fr"] = CommonLanguages.FR_FR, ["french"] = CommonLanguages.FR_FR, ["französisch"] = CommonLanguages.FR_FR, ["français"] = CommonLanguages.FR_FR,
        ["es"] = CommonLanguages.ES_ES, ["spanish"] = CommonLanguages.ES_ES, ["spanisch"] = CommonLanguages.ES_ES, ["español"] = CommonLanguages.ES_ES,
        ["zh"] = CommonLanguages.ZH_CN, ["chinese"] = CommonLanguages.ZH_CN, ["chinesisch"] = CommonLanguages.ZH_CN,
        ["ja"] = CommonLanguages.JA_JP, ["japanese"] = CommonLanguages.JA_JP, ["japanisch"] = CommonLanguages.JA_JP,
        ["ru"] = CommonLanguages.RU_RU, ["russian"] = CommonLanguages.RU_RU, ["russisch"] = CommonLanguages.RU_RU,
        ["hi"] = CommonLanguages.HI_IN, ["hindi"] = CommonLanguages.HI_IN,
    };

    /// <summary>
    /// Imports the rows of a table into the entries of a glossary.
    /// </summary>
    /// <remarks>
    /// Two entries count as the same when their terms are equal, ignoring case and spaces. When the table
    /// holds a term twice, its last row wins, just as a later import replaces an earlier one.
    /// </remarks>
    /// <param name="existingEntries">The entries the glossary holds before the import.</param>
    /// <param name="table">The table to import.</param>
    /// <param name="mapping">Which columns of the table fill which parts of the entries.</param>
    /// <param name="mode">What happens to the entries the glossary holds already.</param>
    /// <returns>All entries of the glossary after the import, and what the import changed.</returns>
    public static GlossaryImportResult Apply(IReadOnlyList<GlossaryEntry> existingEntries, GlossaryImportTable table, GlossaryColumnMapping mapping, GlossaryImportMode mode)
    {
        if (mapping.TermColumn < 0 || mapping.TermColumn >= table.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(mapping), "The column of the terms is not part of the table.");

        if (mapping.ValueColumn < 0 || mapping.ValueColumn >= table.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(mapping), "The column of the values is not part of the table.");

        //
        // The parentheses are needed: without them, the compiler of the .NET 9 SDK reads
        // "GlossaryImportMode.REPLACE? []" as a nullable type with a list pattern.
        //
        List<GlossaryEntry> entries = (mode is GlossaryImportMode.REPLACE) ? [] : existingEntries.ToList();
        var countBefore = entries.Count;
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
            positions.TryAdd(ToKey(entries[index].Term), index);

        var replacedPositions = new HashSet<int>();
        var skippedRows = 0;
        foreach (var row in table.Rows)
        {
            var term = Cell(row, mapping.TermColumn);
            var value = Cell(row, mapping.ValueColumn);
            if (term.Length == 0 || value.Length == 0)
            {
                skippedRows++;
                continue;
            }

            var aliases = Cell(row, mapping.AliasesColumn).Split(ALIAS_SEPARATOR, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var entry = new GlossaryEntry(term, value, aliases.Length > 0 ? aliases : null, Cell(row, mapping.NoteColumn));

            var key = ToKey(term);
            if (positions.TryGetValue(key, out var position))
            {
                entries[position] = entry;
                if (position < countBefore)
                    replacedPositions.Add(position);

                continue;
            }

            positions[key] = entries.Count;
            entries.Add(entry);
        }

        var removed = mode is GlossaryImportMode.REPLACE ? existingEntries.Count : 0;
        return new(entries, entries.Count - countBefore, replacedPositions.Count, removed, skippedRows);
    }

    /// <summary>
    /// Finds the first table which holds all the named columns, and maps them by their header.
    /// </summary>
    /// <remarks>
    /// The headers count regardless of case and of spaces around them, so "Deutsch " in a spreadsheet
    /// matches "deutsch" in a configuration plugin.
    /// </remarks>
    /// <param name="tables">The tables of a file, e.g. one per sheet.</param>
    /// <param name="termColumn">The header of the column with the terms.</param>
    /// <param name="valueColumn">The header of the column with the values.</param>
    /// <param name="otherFormsColumn">The header of the column with the other forms; null when there is none.</param>
    /// <param name="noteColumn">The header of the column with the notes; null when there is none.</param>
    /// <param name="table">The table which holds the columns.</param>
    /// <param name="mapping">The positions of the columns in that table.</param>
    /// <returns>True when a table holds all the named columns.</returns>
    public static bool TryFindColumns(IReadOnlyList<GlossaryImportTable> tables, string termColumn, string valueColumn, string? otherFormsColumn, string? noteColumn, [NotNullWhen(true)] out GlossaryImportTable? table, [NotNullWhen(true)] out GlossaryColumnMapping? mapping)
    {
        foreach (var candidate in tables)
        {
            var termIndex = IndexOfColumn(candidate, termColumn);
            var valueIndex = IndexOfColumn(candidate, valueColumn);
            var otherFormsIndex = otherFormsColumn is null ? null : IndexOfColumn(candidate, otherFormsColumn);
            var noteIndex = noteColumn is null ? null : IndexOfColumn(candidate, noteColumn);
            if (termIndex is null || valueIndex is null || (otherFormsColumn is not null && otherFormsIndex is null) || (noteColumn is not null && noteIndex is null))
                continue;

            table = candidate;
            mapping = new GlossaryColumnMapping(termIndex.Value, valueIndex.Value, otherFormsIndex, noteIndex);
            return true;
        }

        table = null;
        mapping = null;
        return false;
    }

    private static int? IndexOfColumn(GlossaryImportTable table, string header)
    {
        for (var index = 0; index < table.Columns.Count; index++)
            if (string.Equals(table.Columns[index].Trim(), header.Trim(), StringComparison.OrdinalIgnoreCase))
                return index;

        return null;
    }

    /// <summary>
    /// Guesses the language a column holds from its name, e.g. "Deutsch", "English", or "de-DE".
    /// </summary>
    /// <param name="columnName">The name of the column, as the header row states it.</param>
    /// <returns>The language, or null when the name gives none away.</returns>
    public static CommonLanguages? SuggestLanguage(string columnName)
    {
        var name = columnName.Trim().ToLowerInvariant();
        if (name.Length == 0)
            return null;

        foreach (var language in Enum.GetValues<CommonLanguages>())
        {
            if (language is CommonLanguages.AS_IS or CommonLanguages.OTHER)
                continue;

            if (name == language.ToIETFTag().ToLowerInvariant() || name == language.ToString().ToLowerInvariant())
                return language;
        }

        return LANGUAGE_NAMES.TryGetValue(name, out var namedLanguage) ? namedLanguage : null;
    }

    private static string Cell(IReadOnlyList<string> row, int? column) => column is { } index && index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private static string ToKey(string term) => string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}