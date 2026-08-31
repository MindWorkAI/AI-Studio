using System.Diagnostics;
using System.Globalization;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    private async Task StartBatchProcessingAsync()
    {
        var runPreparation = await this.PrepareRunAsync();
        if (runPreparation is null)
            return;

        var (resolvedOutputDirectory, files) = runPreparation.Value;

        //
        // Every format but Markdown is written by Pandoc, so it has to be there before the first
        // document. Asking per document would put the installation dialog in front of the user
        // hundreds of times, and starting without it would spend time and tokens on answers we
        // cannot write anywhere:
        //
        if (this.outputMode is BatchProcessingOutputMode.INDIVIDUAL_FILES && this.resultFileFormat.UsesPandoc())
        {
            var pandocState = await this.PandocAvailability.EnsureAvailabilityAsync(showSuccessMessage: false, showDialog: true);
            if (!pandocState.IsAvailable)
                return;
        }

        //
        // When the output folder already contains a log, a previous run was
        // interrupted or produced errors. Let the user decide what to do:
        //
        var previousLog = new Dictionary<string, BatchProcessingLogEntry>(StringComparer.OrdinalIgnoreCase);
        var previousResults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(Path.Join(resolvedOutputDirectory, LOG_FILENAME)))
        {
            var previousRun = await this.LoadPreviousRunAsync(resolvedOutputDirectory, files);
            if (previousRun is null)
                return;

            (previousLog, previousResults) = previousRun.Value;
        }

        this.PrepareFileResults(resolvedOutputDirectory, files, previousLog, previousResults);
        await this.CheckpointAssistantSession();
        await this.RunBatchAsync(resolvedOutputDirectory);
    }

    private void PrepareFileResults(string resolvedOutputDirectory, IReadOnlyList<string> files, Dictionary<string, BatchProcessingLogEntry> previousLog, Dictionary<string, string> previousResults)
    {
        this.ClearInputIssues();
        this.fileResults.Clear();
        this.usedResultFileNames.Clear();
        this.hasReportedWriteFailure = false;
        this.numProcessedFiles = 0;
        this.pauseBeforeNextFileSeconds = 0;
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(this.inputDirectory, file);
            var fileResult = new BatchProcessingFileResult
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                RelativePath = relativePath,
            };

            var canRestore = this.CanRestoreFromPreviousRun(relativePath, resolvedOutputDirectory, previousLog, previousResults, out var logEntry);
            if (canRestore && logEntry is not null)
            {
                fileResult.Status = BatchProcessingFileStatus.DONE;
                fileResult.Message = logEntry.Details;
                fileResult.ModelName = logEntry.Model;
                fileResult.ResultText = previousResults.GetValueOrDefault(relativePath, string.Empty);

                if (DateTimeOffset.TryParseExact(logEntry.Time, TIME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var processedAt))
                    fileResult.ProcessedAt = processedAt;

                // Reserve the result file name of the previous run, so that a
                // document processed now cannot overwrite that earlier result:
                if (!string.IsNullOrWhiteSpace(logEntry.Details))
                    this.usedResultFileNames.Add(logEntry.Details);

                this.numProcessedFiles++;
            }

            this.fileResults.Add(fileResult);
        }
    }

    /// <summary>
    /// Processes all documents which are not restored from a previous run.
    /// </summary>
    private async Task RunBatchAsync(string resolvedOutputDirectory)
    {
        this.isProcessingBatch = true;
        var stopwatch = Stopwatch.StartNew();
        var delayRange = this.GetEffectiveDelayRange();
        this.Logger.LogInformation(
            "Batch processing started. InputDirectory='{InputDirectory}', OutputDirectory='{OutputDirectory}', TotalFiles={TotalFiles}, RestoredFiles={RestoredFiles}, Model='{Model}', MinimumDelaySeconds={MinimumDelaySeconds}, MaximumDelaySeconds={MaximumDelaySeconds}.",
            this.inputDirectory,
            resolvedOutputDirectory,
            this.fileResults.Count,
            this.fileResults.Count(fileResult => fileResult.Status is BatchProcessingFileStatus.DONE),
            this.ProviderSettings.Model,
            delayRange.Minimum,
            delayRange.Maximum);

        // We use the cancellation token of the assistant base class, which
        // creates it before it calls us and disposes it after we returned.
        // This way, the stop button of the assistant frame cancels the batch
        // run as well, and the base class recognizes the run as canceled.
        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            for (var index = 0; index < this.fileResults.Count; index++)
            {
                var fileResult = this.fileResults[index];

                // Restored from the log of a previous run:
                if (fileResult.Status is BatchProcessingFileStatus.DONE)
                    continue;

                // A requested cancellation stops the loop right away. All
                // remaining files keep their QUEUED state on purpose, so
                // that the UI shows which files were not processed:
                if (token.IsCancellationRequested)
                    break;

                fileResult.Status = BatchProcessingFileStatus.PROCESSING;
                fileResult.ModelName = this.ProviderSettings.Model.ToString();
                await this.CheckpointAssistantSession();
                await this.RefreshAssistantUIAsync();

                await this.ProcessOneFileAsync(fileResult, resolvedOutputDirectory, token);

                this.numProcessedFiles++;
                await this.WriteAggregatedResultsAsync(resolvedOutputDirectory);
                await this.CheckpointAssistantSession();
                await this.RefreshAssistantUIAsync();

                var anotherFileIsWaiting = this.fileResults.Skip(index + 1).Any(nextFile => nextFile.Status is not BatchProcessingFileStatus.DONE);
                if (anotherFileIsWaiting)
                    await this.WaitBeforeNextFileAsync(delayRange.Minimum, delayRange.Maximum, token);
            }
        }
        finally
        {
            stopwatch.Stop();
            var doneFiles = this.fileResults.Count(fileResult => fileResult.Status is BatchProcessingFileStatus.DONE);
            var failedFiles = this.fileResults.Count(fileResult => fileResult.Status is BatchProcessingFileStatus.FAILED);
            var canceledFiles = this.fileResults.Count(fileResult => fileResult.Status is BatchProcessingFileStatus.CANCELED);
            var queuedFiles = this.fileResults.Count(fileResult => fileResult.Status is BatchProcessingFileStatus.QUEUED);

            this.Logger.LogInformation(
                "Batch processing finished after {ElapsedMilliseconds} ms. TotalFiles={TotalFiles}, DoneFiles={DoneFiles}, FailedFiles={FailedFiles}, CanceledFiles={CanceledFiles}, QueuedFiles={QueuedFiles}, OutputWriteFailed={OutputWriteFailed}.",
                stopwatch.ElapsedMilliseconds,
                this.fileResults.Count,
                doneFiles,
                failedFiles,
                canceledFiles,
                queuedFiles,
                this.hasReportedWriteFailure);

            // The cancellation token source belongs to the base class, which
            // disposes it and evaluates its state after we returned:
            this.isProcessingBatch = false;
            await this.CheckpointAssistantSession();
            await this.RefreshAssistantUIAsync();

            if (failedFiles > 0)
            {
                var failureMessage = failedFiles == 1
                    ? T("The batch run finished, but one file could not be processed. See the progress table and log for details.")
                    : string.Format(T("The batch run finished, but {0} files could not be processed. See the progress table and log for details."), failedFiles);
                await this.MessageBus.SendError(new(Icons.Material.Filled.Error, failureMessage));
            }
        }
    }

    /// <summary>
    /// Processes exactly one file and stores any error as the file's result.
    /// </summary>
    /// <remarks>
    /// All stages catch broadly on purpose: one outlier (a locked file, an
    /// unexpected AI answer, a write error) must never stop the entire batch run.
    /// </remarks>
    private async Task ProcessOneFileAsync(BatchProcessingFileResult fileResult, string resolvedOutputDirectory, CancellationToken token)
    {
        var fileContent = await this.LoadInputContentAsync(fileResult, token);
        if (fileContent is null)
            return;

        string aiAnswer;
        try
        {
            aiAnswer = await this.CallAIAsync(fileResult.FileName, fileContent, token);
        }
        catch (OperationCanceledException)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return;
        }
        catch (Exception e)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("The AI request failed: {0}"), e.Message), e);
            return;
        }

        // A cancellation may arrive while the answer is still streaming. The
        // partial answer must not count as a result: it would look complete in
        // the results table, and continuing the run later would skip the document.
        if (token.IsCancellationRequested)
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.CANCELED, T("The batch run was canceled."));
            return;
        }

        if (string.IsNullOrWhiteSpace(aiAnswer))
        {
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("The AI answer was empty."));
            return;
        }

        fileResult.ResultText = aiAnswer;
        if (this.outputMode is BatchProcessingOutputMode.INDIVIDUAL_FILES)
        {
            try
            {
                var resultFilePath = Path.Join(resolvedOutputDirectory, this.CreateResultFileName(fileResult.FileName));
                if (this.resultFileFormat.UsesPandoc())
                {
                    //
                    // Pandoc reports a failure instead of throwing, because one document which
                    // cannot be converted must not end a run over hundreds of them:
                    //
                    if (!await PandocExport.ConvertAsync(this.RustService, aiAnswer, resultFilePath, this.resultFileFormat, token))
                    {
                        this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, T("Was not able to convert the answer into the chosen file format."));
                        return;
                    }
                }
                else
                    await File.WriteAllTextAsync(resultFilePath, aiAnswer, this.resultFileFormat.ToFileEncoding(), CancellationToken.None);

                this.FinishFileResult(fileResult, BatchProcessingFileStatus.DONE, Path.GetFileName(resultFilePath));
            }
            catch (Exception e)
            {
                this.FinishFileResult(fileResult, BatchProcessingFileStatus.FAILED, string.Format(T("Was not able to write the result file: {0}"), e.Message), e);
            }
        }
        else
            this.FinishFileResult(fileResult, BatchProcessingFileStatus.DONE, string.Empty);
    }

    private void FinishFileResult(BatchProcessingFileResult fileResult, BatchProcessingFileStatus status, string message, Exception? exception = null)
    {
        fileResult.Status = status;
        fileResult.Message = message;
        fileResult.ProcessedAt = DateTimeOffset.Now;

        if (status is not BatchProcessingFileStatus.FAILED)
            return;

        if (exception is null)
            this.Logger.LogWarning("Batch processing of file '{FilePath}' failed: {Message}", fileResult.FilePath, message);
        else
            this.Logger.LogError(exception, "Batch processing of file '{FilePath}' failed: {Message}", fileResult.FilePath, message);
    }

    private async Task CancelBatchProcessingAsync()
    {
        await this.CancelAssistantSessionAsync();
    }
}