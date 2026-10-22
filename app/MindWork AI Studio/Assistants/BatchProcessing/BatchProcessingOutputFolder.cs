namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// Tells the search for documents which of the files it finds a batch run wrote itself.
/// </summary>
/// <remarks>
/// A run writes its result files, its log, and its results table directly into the output folder.
/// What the search must leave out therefore depends on where this folder lies. Below the input
/// folder, the search leaves out all of it. When it is the input folder itself, leaving out the
/// whole folder would leave no document at all, so the search leaves out what a run writes, by
/// name. Anywhere else, above the input folder included, the search never comes across the output
/// folder and leaves out nothing.
/// </remarks>
/// <param name="inputDirectory">The folder the run reads its documents from.</param>
/// <param name="outputDirectory">The folder the run writes into.</param>
/// <param name="resultsFileName">The file name of the results table.</param>
internal sealed class BatchProcessingOutputFolder(string inputDirectory, string outputDirectory, string resultsFileName)
{
    private readonly string fullOutputDirectory = Path.GetFullPath(outputDirectory);

    private readonly bool isInputFolder = IsSameFolder(inputDirectory, outputDirectory);

    //
    // The same folder with a separator at its end would count as lying below itself:
    //
    private readonly bool liesBelowInputFolder = !IsSameFolder(inputDirectory, outputDirectory) && PathTools.IsInsideDirectory(inputDirectory, outputDirectory);

    /// <summary>
    /// Whether the search enters a subfolder of the input folder: every one but the output folder.
    /// </summary>
    /// <param name="directory">The subfolder.</param>
    public bool Enters(string directory) => !this.liesBelowInputFolder || !IsSameFolder(directory, this.fullOutputDirectory);

    /// <summary>
    /// Whether a file the search found was written by a run instead of being a document.
    /// </summary>
    /// <param name="filePath">The full path of the file.</param>
    public bool IsOutputOfRun(string filePath)
    {
        //
        // Checked with the separator after the name of the folder: otherwise, an output folder
        // named 'out' would also leave out a document named 'output-notes.md':
        //
        if (this.liesBelowInputFolder)
            return PathTools.IsInsideDirectory(this.fullOutputDirectory, filePath);

        //
        // A run writes into the output folder itself, never into one of its subfolders:
        //
        return this.isInputFolder
               && Path.GetDirectoryName(Path.GetFullPath(filePath)) is { } fileDirectory
               && IsSameFolder(fileDirectory, this.fullOutputDirectory)
               && this.IsOwnOutputArtifact(filePath);
    }

    /// <summary>
    /// Checks whether a file is an output artifact of the assistant. We need
    /// this when the output folder is the input folder: without it, the results
    /// of a previous run would be processed as documents.
    /// </summary>
    /// <remarks>
    /// A result file is recognized by its name without the extension, e.g., report_result.md or
    /// report_result_2.docx, cf. AssistantBatchProcessing.CreateResultFileName. Its extension does
    /// not count, because the user may have chosen another file format since a previous run.
    /// </remarks>
    private bool IsOwnOutputArtifact(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.Equals(fileName, AssistantBatchProcessing.LOG_FILENAME, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, resultsFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var suffixIndex = stem.LastIndexOf(AssistantBatchProcessing.RESULT_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);
        if (suffixIndex < 0)
            return false;

        //
        // A second result of the same name gets a counter, e.g., report_result_2.md:
        //
        var counter = stem[(suffixIndex + AssistantBatchProcessing.RESULT_FILE_SUFFIX.Length)..];
        return counter.Length is 0 || (counter.Length > 1 && counter[0] is '_' && counter[1..].All(char.IsAsciiDigit));
    }

    private static bool IsSameFolder(string firstDirectory, string secondDirectory) => string.Equals(TrimDirectorySeparator(Path.GetFullPath(firstDirectory)), TrimDirectorySeparator(Path.GetFullPath(secondDirectory)), PathTools.COMPARISON);

    private static string TrimDirectorySeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}