using System.Globalization;
using System.Text;

using AIStudio.Dialogs;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    /// <summary>
    /// Asks the user whether a previous batch run should be continued.
    /// </summary>
    /// <returns>The decision, or <c>null</c> when the user canceled the dialog.</returns>
    private async Task<BatchProcessingResumeDecision?> AskResumeDecisionAsync(int numCompletedFiles, int numRemainingFiles, int numMissingResults)
    {
        var dialogParameters = new DialogParameters<BatchProcessingResumeDialog>
        {
            { x => x.NumCompletedFiles, numCompletedFiles },
            { x => x.NumRemainingFiles, numRemainingFiles },
            { x => x.NumMissingResults, numMissingResults },
        };

        var dialogReference = await this.DialogService.ShowAsync<BatchProcessingResumeDialog>(T("Continue the previous batch run?"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            return null;

        return dialogResult.Data as BatchProcessingResumeDecision?;
    }

    /// <summary>
    /// Reads the log of the previous run and asks the user how to proceed.
    /// </summary>
    /// <returns>The previous log and results, or <c>null</c> when the user canceled.</returns>
    private async Task<(Dictionary<string, BatchProcessingLogEntry> PreviousLog, Dictionary<string, string> PreviousResults)?> LoadPreviousRunAsync(string resolvedOutputDirectory, IReadOnlyList<string> files)
    {
        var previousLog = await this.ReadLogAsync(Path.Join(resolvedOutputDirectory, LOG_FILENAME));

        // We read the results table before showing the dialog: the dialog must
        // report how many documents are actually restorable, not how many the
        // log claims to be completed. Both may differ, e.g., when the user
        // deleted result files or renamed the results table in the meantime.
        var previousResults = this.outputMode is BatchProcessingOutputMode.TABLE_ONLY
            ? await this.ReadPreviousResultsAsync(Path.Join(resolvedOutputDirectory, this.ResolveResultsFileName()))
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var numCompletedInLog = 0;
        var numRestorable = 0;
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(this.inputDirectory, file);
            if (previousLog.TryGetValue(relativePath, out var entry) && entry.WasSuccessful)
                numCompletedInLog++;

            if (this.CanRestoreFromPreviousRun(relativePath, resolvedOutputDirectory, previousLog, previousResults, out _))
                numRestorable++;
        }

        var decision = await this.AskResumeDecisionAsync(numRestorable, files.Count - numRestorable, numCompletedInLog - numRestorable);
        if (decision is null)
            return null;

        if (decision is BatchProcessingResumeDecision.RESTART)
            previousLog.Clear();

        return (previousLog, previousResults);
    }

    /// <summary>
    /// Checks whether a document can be restored from the previous run. Beyond
    /// the log entry, the result of the previous run must still exist: in the
    /// table mode the answer within the results table, in the individual file
    /// mode the result file. Without the result, restoring would mark the document as
    /// done while its answer is lost, so we process it again instead.
    /// </summary>
    private bool CanRestoreFromPreviousRun(string relativePath, string resolvedOutputDirectory, Dictionary<string, BatchProcessingLogEntry> previousLog, Dictionary<string, string> previousResults, out BatchProcessingLogEntry? logEntry)
    {
        if (!previousLog.TryGetValue(relativePath, out logEntry) || !logEntry.WasSuccessful)
            return false;

        if (this.outputMode is BatchProcessingOutputMode.TABLE_ONLY)
            return previousResults.ContainsKey(relativePath);

        return !string.IsNullOrWhiteSpace(logEntry.Details) && File.Exists(Path.Join(resolvedOutputDirectory, logEntry.Details));
    }

    /// <summary>
    /// Rewrites the output files after each processed file. This way, the
    /// results on disk stay complete even when the run is canceled or crashes.
    /// </summary>
    private async Task WriteAggregatedResultsAsync(string resolvedOutputDirectory)
    {
        await this.WriteLogAsync(resolvedOutputDirectory);

        if (this.outputMode is BatchProcessingOutputMode.TABLE_ONLY)
            await this.WriteResultsTableAsync(resolvedOutputDirectory);
    }

    /// <summary>
    /// Writes the log of the batch run. The log contains the metadata of every
    /// document, including the documents which failed. It never contains the AI
    /// answers, and it is written in both output modes.
    /// </summary>
    private async Task WriteLogAsync(string resolvedOutputDirectory)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CsvWriter.ToRow(LOG_SEPARATOR, T("File"), T("Time"), T("Model"), T("Status"), T("Details")));
        foreach (var fileResult in this.fileResults.Where(x => x.Status is not BatchProcessingFileStatus.QUEUED and not BatchProcessingFileStatus.PROCESSING))
            sb.AppendLine(CsvWriter.ToRow(LOG_SEPARATOR, fileResult.RelativePath, fileResult.ProcessedAt.ToString(TIME_FORMAT, CultureInfo.InvariantCulture), fileResult.ModelName, fileResult.Status.ToString(), fileResult.Message));

        await this.WriteCsvFileAsync(Path.Join(resolvedOutputDirectory, LOG_FILENAME), sb.ToString());
    }

    /// <summary>
    /// Writes the results table, which contains the AI answers.
    /// </summary>
    private async Task WriteResultsTableAsync(string resolvedOutputDirectory)
    {
        var separator = this.csvSeparator.Character(this.customCsvSeparator);
        var sb = new StringBuilder();
        sb.AppendLine(CsvWriter.ToRow(separator, T("File"), this.ResultColumnHeader));
        foreach (var fileResult in this.fileResults.Where(x => x.Status is BatchProcessingFileStatus.DONE))
            sb.AppendLine(CsvWriter.ToRow(separator, fileResult.RelativePath, fileResult.ResultText));

        await this.WriteCsvFileAsync(Path.Join(resolvedOutputDirectory, this.ResolveResultsFileName()), sb.ToString());
    }

    private async Task WriteCsvFileAsync(string targetFilePath, string content)
    {
        // Write to a sibling file first, then rename. This way, an aborted
        // write can never destroy the results of the previous files:
        var tempFilePath = targetFilePath + ".tmp";
        try
        {
            // We write the CSV file with a byte order mark, so that spreadsheet
            // applications recognize the UTF-8 encoding of, e.g., umlauts:
            await File.WriteAllTextAsync(tempFilePath, content, new UTF8Encoding(true), CancellationToken.None);
            File.Move(tempFilePath, targetFilePath, true);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Was not able to write the batch output file '{TargetFilePath}'.", targetFilePath);

            // Remove our leftover: a failing rename keeps the temporary file in
            // the output folder, where it looks like a result to the user and
            // piles up over several runs.
            try
            {
                File.Delete(tempFilePath);
            }
            catch (Exception deleteError)
            {
                this.Logger.LogWarning(deleteError, "Was not able to remove the temporary file '{TempFilePath}'.", tempFilePath);
            }

            // A failing write repeats for every document. We report it once per
            // run: without any message, the UI would show a successful run
            // while the files on disk stay behind.
            if (this.hasReportedWriteFailure)
                return;

            this.hasReportedWriteFailure = true;
            await this.MessageBus.SendError(new(Icons.Material.Filled.SaveAs, string.Format(T("Was not able to write '{0}'. Please make sure that the file is not opened in another application. The results of this run are incomplete on disk. The message is: '{1}'"), Path.GetFileName(targetFilePath), e.Message)));
        }
    }

    /// <summary>
    /// Reads the log of a previous batch run. The key is the relative path of
    /// the document.
    /// </summary>
    private async Task<Dictionary<string, BatchProcessingLogEntry>> ReadLogAsync(string logFilePath)
    {
        var entries = new Dictionary<string, BatchProcessingLogEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var content = await File.ReadAllTextAsync(logFilePath);
            var rows = BatchProcessingCsv.ParseWithDetectedSeparator(content, 5, LOG_SEPARATOR, '|');

            // The first row is the header, which we skip:
            foreach (var row in rows.Skip(1))
            {
                if (row.Count < 5 || string.IsNullOrWhiteSpace(row[0]))
                    continue;

                entries[row[0]] = new BatchProcessingLogEntry(row[0], row[1], row[2], row[3], row[4]);
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Was not able to read the log of the previous batch run at '{LogFilePath}'.", logFilePath);

            // Without this message, continuing the run would silently process
            // every document again, because we recognize nothing as completed:
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, T("Was not able to read the log of the previous run. Continuing the run would process all documents again.")));
        }

        return entries;
    }

    /// <summary>
    /// Reads the AI answers of a previous batch run from the results table, so
    /// that continuing a run does not lose the answers of the previous run.
    /// </summary>
    private async Task<Dictionary<string, string>> ReadPreviousResultsAsync(string resultsFilePath)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(resultsFilePath))
                return results;

            var content = await File.ReadAllTextAsync(resultsFilePath);
            var configuredSeparator = this.csvSeparator.Character(this.customCsvSeparator);
            var rows = BatchProcessingCsv.ParseWithDetectedSeparator(content, 2, configuredSeparator, ';', '|', ',', '\t');
            foreach (var row in rows.Skip(1))
            {
                if (row.Count < 2 || string.IsNullOrWhiteSpace(row[0]))
                    continue;

                results[row[0]] = row[1];
            }
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Was not able to read the results table of the previous batch run at '{ResultsFilePath}'.", resultsFilePath);
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Warning, T("Was not able to read the results table of the previous run. Its completed documents cannot be restored and will be processed again.")));
        }

        return results;
    }

    /// <summary>
    /// Creates the name of the result file for one document, in the chosen file format.
    /// </summary>
    /// <remarks>
    /// Two documents of the same run may share their name and differ only in
    /// their extension, e.g., report.docx and report.pdf. Both would map to
    /// report_result.md, so we add a counter for the second one. Otherwise, one
    /// result would silently overwrite the other.
    /// </remarks>
    private string CreateResultFileName(string sourceFileName)
    {
        var extension = this.resultFileFormat.ToFileExtension();
        var stem = Path.GetFileNameWithoutExtension(sourceFileName);
        var candidate = $"{stem}{RESULT_FILE_SUFFIX}{extension}";

        var counter = 2;
        while (!this.usedResultFileNames.Add(candidate))
        {
            candidate = $"{stem}{RESULT_FILE_SUFFIX}_{counter}{extension}";
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Resolves the file name of the CSV results table. This is the only output
    /// file the user may name; the log always uses <see cref="LOG_FILENAME"/>.
    /// </summary>
    private string ResolveResultsFileName()
    {
        var name = this.csvFileName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return DEFAULT_RESULTS_FILENAME;

        return name.EndsWith(CSV_EXTENSION, StringComparison.OrdinalIgnoreCase) ? name : $"{name}{CSV_EXTENSION}";
    }
}