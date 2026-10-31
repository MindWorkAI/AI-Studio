using System.Text.RegularExpressions;

using Markdig.Extensions.Mathematics;
using Markdig.Syntax;

namespace AIStudio.Assistants.ERI;

/// <summary>
/// Reads the files of an ERI server out of the answers of the model.
/// </summary>
internal static partial class ERIServerFiles
{
    [GeneratedRegex("""
                    "([\\/._\w-]+)"
                    """, RegexOptions.NonBacktracking)]
    private static partial Regex FileExtractRegex();

    /// <summary>
    /// Reads which files the model wants to create.
    /// </summary>
    /// <param name="fileListAnswer">The answer of the model to the request for the list of files.</param>
    /// <returns>The paths of the files, relative to the base directory.</returns>
    public static IEnumerable<string> ReadFileList(string fileListAnswer)
    {
        //
        // We asked the LLM for answering using a specific JSON scheme.
        // However, the LLM might not follow this scheme. Therefore, we
        // need to parse the answer and extract the files.
        // The parsing strategy is to look for all strings.
        //
        var matches = FileExtractRegex().Matches(fileListAnswer);
        foreach (Match match in matches)
            if(match.Groups[1].Value is {} file && !string.IsNullOrWhiteSpace(file) && !file.Equals("files", StringComparison.OrdinalIgnoreCase))
                yield return file;
    }

    /// <summary>
    /// Reads the content of a file out of the answer which wrote it.
    /// </summary>
    /// <remarks>
    /// The model is asked for a heading with the path of the file, followed by a code block with its
    /// content. Only the code block counts: models write the heading as they please, e.g., with the
    /// path in backticks, with a "File:" in front, or not at all. Up to v26.10.1, a regular expression
    /// insisted on a heading without a hyphen, which left out files such as docker-compose.yml, and it
    /// read up to the last fence of the answer, which merged a usage example into the file.
    ///
    /// Markdig reads the answer the way the chat shows it. So we take the first complete code block,
    /// and a file which holds code blocks itself stays whole when the model fences it with more
    /// backticks. A block the model never closed is left out: the answer broke off, and half a file
    /// is nothing to write to disk.
    /// </remarks>
    /// <param name="answer">The answer of the model to the request for one file.</param>
    /// <returns>The content of the file, ending with a line break, or an empty string when the
    /// answer holds no complete code block.</returns>
    public static string ReadFileContent(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
            return string.Empty;

        //
        // Markdig reads a formula between $$ as a MathBlock, which is a fenced code block as well:
        //
        var document = Markdig.Markdown.Parse(answer, Markdown.SAFE_MARKDOWN_PIPELINE);
        var codeBlock = document.Descendants<FencedCodeBlock>().FirstOrDefault(block => block is not MathBlock && block.ClosingFencedCharCount > 0);
        if (codeBlock is null)
            return string.Empty;

        var content = codeBlock.Lines.ToString();
        return content.Length is 0 ? string.Empty : $"{content}\n";
    }
}