using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Reads a spreadsheet or CSV file whose rows the user imports into a glossary.
/// </summary>
/// <remarks>
/// The runtime extracts the file, so its content passes the prompt injection filter like every other
/// file before any of it can reach a model.
/// </remarks>
public sealed class GlossaryImportService(RustService rustService, ILogger<GlossaryImportService> logger)
{
    /// <summary>
    /// Reads the tables of a file.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="reportingScope">The action the file is read for.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The tables of the file, or why there are none.</returns>
    public async Task<GlossaryImportFile> ReadFileAsync(string filePath, PromptInjectionReportingScope? reportingScope, CancellationToken token = default)
    {
        FileInfo file;
        try
        {
            file = new FileInfo(filePath);
            if (!file.Exists)
            {
                logger.LogWarning("The glossary file '{Path}' does not exist.", filePath);
                return GlossaryImportFile.Failed(GlossaryImportIssue.FILE_NOT_FOUND);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "The glossary file '{Path}' is not accessible.", filePath);
            return GlossaryImportFile.Failed(GlossaryImportIssue.FILE_NOT_FOUND);
        }

        var extraction = await rustService.ReadArbitraryFileData(file.FullName, int.MaxValue, reportingScope: reportingScope, token: token);
        if (extraction.Outcome is FileExtractionOutcome.FAILED)
        {
            logger.LogWarning("The glossary file '{Path}' could not be read: {ErrorCode}.", file.FullName, extraction.ErrorCode);
            return GlossaryImportFile.Failed(GlossaryImportIssue.UNREADABLE);
        }

        var tables = GlossaryParser.Parse(extraction.Content, IsDelimitedTextFile(file));
        if (tables.Count == 0)
        {
            logger.LogWarning("The glossary file '{Path}' holds no table with a header row, two columns, and one row.", file.FullName);
            return GlossaryImportFile.Failed(GlossaryImportIssue.NO_TABLE);
        }

        return new(GlossaryImportIssue.NONE, tables);
    }

    //
    // The extensions the runtime reads as delimited text rather than as a spreadsheet, see
    // ExtractionRoute::Csv in runtime/src/file_data.rs:
    //
    private static bool IsDelimitedTextFile(FileInfo file) => file.Extension.ToLowerInvariant() is ".csv" or ".tsv";
}