using AIStudio.Settings;
using AIStudio.Tools.Media;

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
        var outputFolder = new BatchProcessingOutputFolder(this.inputDirectory, resolvedOutputDirectory, this.ResolveResultsFileName());
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        this.numTooLargeFiles = 0;

        foreach (var file in selection.EnumerateCandidates(
                     this.inputDirectory,
                     includeDirectory: outputFolder.Enters,
                     reportInaccessibleDirectory: (directory, exception) =>
                         throw new IOException($"Could not access directory '{directory}'.", exception)))
        {
            var normalizedFile = Path.GetFullPath(file);
            if (!selection.Selects(normalizedFile) || IsTranscriptArtifact(normalizedFile) || outputFolder.IsOutputOfRun(normalizedFile))
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

    /// <summary>
    /// Checks for persistent or interrupted media transcript artifacts. They
    /// always live beside their source file, independently of the output folder.
    /// </summary>
    private static bool IsTranscriptArtifact(string filePath)
    {
        return MediaSidecarTranscripts.IsArtifactPath(filePath);
    }

    /// <summary>
    /// Validates the form, finds the documents, and creates the output folder.
    /// </summary>
    /// <returns>The output folder and the documents, or <c>null</c> when the run must not start.</returns>
    private async Task<(string ResolvedOutputDirectory, IReadOnlyList<string> Files)?> PrepareRunAsync()
    {
        await this.Form!.ValidateAsync();

        //
        // The exclusion list sits in the expert settings, which are folded away by default. Unfold
        // them, so the user sees the message next to the list. Only the editor knows about an invalid
        // draft, since it passes on valid lists only:
        //
        if (this.exclusionEditor is { IsValid: false })
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

        //
        // A model that cannot read images would get the text of the request alone, without the
        // image, and answer about nothing. Such images fail one by one, see LoadImageAsync, while the
        // documents next to them are processed. A run of nothing but images does not start at all:
        //
        if (files.All(IsImage) && !this.ProviderSettings.SupportsImageInput())
        {
            this.AddInputIssue(T("All selected files are images, but the selected model cannot read images. Select a model with image support."));
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