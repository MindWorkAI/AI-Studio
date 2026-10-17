
namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    private string? ValidateInputDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return T("Please select the folder that contains the documents you want to process.");

        if (!Directory.Exists(directory))
            return T("The selected folder does not exist.");

        return null;
    }

    private string? ValidateFilePatterns(string patterns) => FileNamePatterns.Validate(patterns);

    private string? ValidateExcludePatterns(string patterns) => FileExclusions.Validate(patterns);

    private string? ValidateCsvFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (fileName.Trim().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return T("Please provide a file name without a path, e.g., my-results.csv");

        return null;
    }

    private string? ValidateCustomCsvSeparator(string separator)
    {
        if (this.outputMode is not BatchProcessingOutputMode.TABLE_ONLY
            || this.csvSeparator is not BatchProcessingCsvSeparator.CUSTOM)
            return null;

        if (!BatchProcessingCsvSeparatorExtensions.IsValidCustomSeparator(separator))
            return T("Please enter exactly one punctuation or symbol character. Letters, numbers, spaces, quotation marks, and line breaks cannot be used as CSV separators.");

        return null;
    }

    private string? ValidateFreePrompt(string prompt)
    {
        if (this.promptSource is BatchProcessingPromptSource.FREE_PROMPT && string.IsNullOrWhiteSpace(prompt))
            return T("Please describe what the AI should do with each document.");

        return null;
    }

    /// <summary>
    /// Validates the instruction sources which have no input field of their own.
    /// </summary>
    private string? ValidateInstructionSource() => this.promptSource switch
    {
        BatchProcessingPromptSource.POLICY when this.ConfiguredPolicyIsMissing => T("The configured default policy no longer exists. Please select another document analysis policy."),
        BatchProcessingPromptSource.POLICY when this.selectedPolicy is null => T("Please select a document analysis policy."),
        BatchProcessingPromptSource.FILE_IMPORT when !string.IsNullOrWhiteSpace(this.promptFileLoadIssue) => this.promptFileLoadIssue,
        BatchProcessingPromptSource.FILE_IMPORT when string.IsNullOrWhiteSpace(this.importedPrompt) => T("Please select the file which contains your instructions."),

        _ => null,
    };

    private string? ValidatingProviderWithBatchState(AIStudio.Settings.Provider provider)
    {
        if (this.isProcessingBatch)
            return null;

        return this.ValidatingProvider(provider);
    }

    private string ResolveOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(this.outputDirectory))
            return Path.Join(this.inputDirectory, DEFAULT_OUTPUT_DIRECTORY_NAME);

        return this.outputDirectory;
    }

    private IReadOnlyList<string> FindInputFiles(string resolvedOutputDirectory)
    {
        var selection = new FolderFileSelection(FileNamePatterns.Parse(this.filePatterns), FileExclusions.Parse(this.excludePatterns), this.includeSubdirectories, this.includeHiddenFilesAndFolders, this.maxFileSizeMegabytes);
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        this.numTooLargeFiles = 0;

        var normalizedInputDirectory = TrimDirectorySeparator(Path.GetFullPath(this.inputDirectory));
        var normalizedOutputDirectory = TrimDirectorySeparator(Path.GetFullPath(resolvedOutputDirectory));

        // When the output folder is a folder of its own, we skip everything
        // inside it. When it is the input folder itself, we must not skip the
        // whole folder: we would not find any document at all. We then skip
        // our own output artifacts instead.
        var isOutputSeparateFolder = !string.Equals(normalizedInputDirectory, normalizedOutputDirectory, StringComparison.OrdinalIgnoreCase);

        // The separator is essential: without it, an output folder named 'out'
        // would also exclude a document named 'output-notes.md':
        var outputDirectoryPrefix = normalizedOutputDirectory + Path.DirectorySeparatorChar;

        foreach (var file in selection.EnumerateCandidates(
                     this.inputDirectory,
                     includeDirectory: directory => !isOutputSeparateFolder
                                                    || !string.Equals(TrimDirectorySeparator(Path.GetFullPath(directory)), normalizedOutputDirectory, StringComparison.OrdinalIgnoreCase),
                     reportInaccessibleDirectory: (directory, exception) =>
                         throw new IOException($"Could not access directory '{directory}'.", exception)))
        {
            var normalizedFile = Path.GetFullPath(file);
            if (!selection.Selects(normalizedFile) || IsTranscriptArtifact(normalizedFile))
                continue;

            if (isOutputSeparateFolder)
            {
                if (normalizedFile.StartsWith(outputDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else if (this.IsOwnOutputArtifact(normalizedFile))
                continue;

            //
            // Left out without a status of its own: the file never becomes part of the run. The
            // line below the file list tells the user, and the log names the limit:
            //
            if (selection.IsTooLarge(new FileInfo(normalizedFile)))
            {
                this.numTooLargeFiles++;
                continue;
            }

            files.Add(normalizedFile);
        }

        if (this.numTooLargeFiles > 0)
            this.Logger.LogInformation("Left out {NumTooLargeFiles} matching file(s) in '{InputDirectory}' from the batch run because they are larger than {MaxFileSize}.", this.numTooLargeFiles, this.inputDirectory, selection.MaxFileSizeBytes.FileSize());

        return [.. files];
    }

    private static string TrimDirectorySeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Checks whether a file is an output artifact of this assistant. We need
    /// this when the output folder is the input folder: without it, the results
    /// of a previous run would be processed as documents.
    /// </summary>
    private bool IsOwnOutputArtifact(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.Equals(fileName, LOG_FILENAME, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, this.ResolveResultsFileName(), StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.EndsWith(RESULT_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks for persistent or interrupted media transcript artifacts. They
    /// always live beside their source file, independently of the output folder.
    /// </summary>
    private static bool IsTranscriptArtifact(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.EndsWith(TRANSCRIPT_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(TRANSCRIPT_FILE_SUFFIX + ".tmp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates the form, finds the documents, and creates the output folder.
    /// </summary>
    /// <returns>The output folder and the documents, or <c>null</c> when the run must not start.</returns>
    private async Task<(string ResolvedOutputDirectory, IReadOnlyList<string> Files)?> PrepareRunAsync()
    {
        await this.Form!.Validate();

        //
        // The exclusion list sits in the expert settings, which are folded away by default. Unfold
        // them, so the user sees the message next to the list:
        //
        if (this.ValidateExcludePatterns(this.excludePatterns) is not null)
            this.showExpertSettings = true;

        var instructionIssue = this.ValidateInstructionSource();
        if (instructionIssue is not null)
        {
            this.AddInputIssue(instructionIssue);
            return null;
        }

        if (!this.InputIsValid)
            return null;

        var resolvedOutputDirectory = this.ResolveOutputDirectory();
        IReadOnlyList<string> files;
        try
        {
            files = this.FindInputFiles(resolvedOutputDirectory);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Was not able to enumerate batch input files in '{InputDirectory}'.", this.inputDirectory);
            this.AddInputIssue(string.Format(T("Was not able to read the input folder: {0}"), e.Message));
            return null;
        }

        if (files.Count == 0)
        {
            this.AddInputIssue(this.numTooLargeFiles > 0
                ? string.Format(T("Every matching file in the selected folder is larger than {0}. Raise the size limit to process them."), FolderFileSelection.MegabytesToBytes(this.maxFileSizeMegabytes).FileSize())
                : T("No matching files were found in the selected folder."));
            return null;
        }

        var requiresTranscription = files.Any(file => IsTranscribableMedia(file) && !HasReusableTranscript(file));
        if (requiresTranscription && !this.MediaTranscriptionService.HasUsableTranscriptionProvider)
        {
            this.AddInputIssue(T("The selected files include audio or video without an existing transcript, but no usable transcription provider is configured. Configure one in the transcription settings or remove the media patterns."));
            return null;
        }

        try
        {
            Directory.CreateDirectory(resolvedOutputDirectory);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Was not able to create the batch output folder '{OutputDirectory}'.", resolvedOutputDirectory);
            this.AddInputIssue(string.Format(T("Was not able to create the output folder: {0}"), e.Message));
            return null;
        }

        return (resolvedOutputDirectory, files);
    }
}