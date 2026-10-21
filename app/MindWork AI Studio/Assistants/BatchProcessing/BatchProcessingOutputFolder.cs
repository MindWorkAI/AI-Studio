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

        return this.isInputFolder && this.IsOwnOutputArtifact(filePath);
    }

    /// <summary>
    /// Checks whether a file is an output artifact of the assistant. We need
    /// this when the output folder is the input folder: without it, the results
    /// of a previous run would be processed as documents.
    /// </summary>
    private bool IsOwnOutputArtifact(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.Equals(fileName, AssistantBatchProcessing.LOG_FILENAME, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(fileName, resultsFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.EndsWith(AssistantBatchProcessing.RESULT_FILE_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameFolder(string firstDirectory, string secondDirectory) => string.Equals(TrimDirectorySeparator(Path.GetFullPath(firstDirectory)), TrimDirectorySeparator(Path.GetFullPath(secondDirectory)), PathTools.COMPARISON);

    private static string TrimDirectorySeparator(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}