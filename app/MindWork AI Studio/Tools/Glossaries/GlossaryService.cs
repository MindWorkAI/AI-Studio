using System.Collections.Concurrent;

using AIStudio.Tools.Security;
using AIStudio.Tools.Services;

namespace AIStudio.Tools.Glossaries;

/// <summary>
/// Reads glossary files and keeps their tables while the files stay unchanged.
/// </summary>
/// <remarks>
/// The live translation translates after every pause in typing. Reading a spreadsheet each time would make
/// every translation wait for the runtime, so a file is read again only once it was changed.
/// </remarks>
public sealed class GlossaryService(RustService rustService, PromptInjectionGuardService guardService, ILogger<GlossaryService> logger)
{
    private readonly ConcurrentDictionary<string, CachedGlossary> cache = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Reads the tables of a glossary file.
    /// </summary>
    /// <param name="filePath">The path of the glossary file.</param>
    /// <param name="reportingScope">The action the glossary is read for.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The tables of the file, or why there are none.</returns>
    public async Task<GlossaryLoadResult> LoadAsync(string filePath, PromptInjectionReportingScope? reportingScope, CancellationToken token = default)
    {
        FileInfo file;
        try
        {
            file = new FileInfo(filePath);
            if (!file.Exists)
            {
                logger.LogWarning("The glossary file '{Path}' does not exist.", filePath);
                return GlossaryLoadResult.Failed(GlossaryLoadIssue.FILE_NOT_FOUND);
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "The glossary file '{Path}' is not accessible.", filePath);
            return GlossaryLoadResult.Failed(GlossaryLoadIssue.FILE_NOT_FOUND);
        }

        var fullPath = file.FullName;
        if (this.cache.TryGetValue(fullPath, out var cached) && cached.LastWriteTimeUtc == file.LastWriteTimeUtc && cached.Length == file.Length)
        {
            //
            // The content was filtered when the file was read. The passages are still missing, so the
            // user hears about them with this action as well, just as when the file was read again:
            //
            if (cached.PromptInjectionRedactedCount > 0)
                await guardService.ReportAsync(new(PromptInjectionSource.FileContent(fullPath), cached.PromptInjectionFindings, cached.PromptInjectionRedactedCount), reportingScope);

            return new(GlossaryLoadIssue.NONE, cached.Tables);
        }

        var extraction = await rustService.ReadArbitraryFileData(fullPath, int.MaxValue, reportingScope: reportingScope, token: token);
        if (extraction.Outcome is FileExtractionOutcome.FAILED)
        {
            logger.LogWarning("The glossary file '{Path}' could not be read: {ErrorCode}.", fullPath, extraction.ErrorCode);
            return GlossaryLoadResult.Failed(GlossaryLoadIssue.UNREADABLE);
        }

        var tables = GlossaryParser.Parse(extraction.Content);
        if (tables.Count == 0)
        {
            logger.LogWarning("The glossary file '{Path}' holds no table with a header row, two columns, and one entry.", fullPath);
            return GlossaryLoadResult.Failed(GlossaryLoadIssue.NO_TABLE);
        }

        this.cache[fullPath] = new(file.LastWriteTimeUtc, file.Length, tables, extraction.PromptInjectionFindings, extraction.PromptInjectionRedactedCount);
        return new(GlossaryLoadIssue.NONE, tables);
    }

    private sealed record CachedGlossary(DateTime LastWriteTimeUtc, long Length, IReadOnlyList<GlossaryTable> Tables, IReadOnlyList<PromptInjectionFinding> PromptInjectionFindings, int PromptInjectionRedactedCount);
}