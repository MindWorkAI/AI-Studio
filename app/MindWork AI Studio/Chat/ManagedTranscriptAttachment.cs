using System.Text;

using AIStudio.Settings;

namespace AIStudio.Chat;

/// <summary>
/// Attachment whose Markdown file is owned and lifecycle-managed by the media feature.
/// </summary>
/// <param name="FileName">Display file name.</param>
/// <param name="FilePath">Absolute staged or chat-owned path.</param>
/// <param name="FileSizeBytes">Current file size.</param>
/// <param name="OriginalFileName">Original media file name used in the title and stem.</param>
/// <param name="IsStaged">Whether the file still lives in operation staging.</param>
public sealed record ManagedTranscriptAttachment(string FileName, string FilePath, long FileSizeBytes, string OriginalFileName, bool IsStaged)
    : FileAttachment(FileAttachmentType.DOCUMENT, FileName, FilePath, FileSizeBytes)
{
    /// <summary>Refreshes the path-derived name and current file size.</summary>
    public override FileAttachment Normalize()
    {
        var size = File.Exists(this.FilePath) ? new FileInfo(this.FilePath).Length : 0;
        return this with { FileName = Path.GetFileName(this.FilePath), FileSizeBytes = size };
    }

    /// <summary>Creates a transcript in an operation-specific staging directory.</summary>
    /// <param name="originalPath">Original media path.</param>
    /// <param name="transcript">Provider transcript.</param>
    /// <returns>The staged managed attachment.</returns>
    public static async Task<ManagedTranscriptAttachment> CreateStagedAsync(string originalPath, string transcript)
    {
        var operationDirectory = Path.Combine(SettingsManager.DataDirectory!, "media-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationDirectory);
        var originalFileName = Path.GetFileName(originalPath);
        var stagingPath = Path.Combine(operationDirectory, $"{Guid.NewGuid():N}.md");
        await WriteMarkdownAsync(stagingPath, originalFileName, transcript);
        return FromPath(stagingPath, originalFileName, isStaged: true);
    }

    /// <summary>Writes transcript Markdown to a temporary file and atomically publishes it.</summary>
    /// <param name="targetPath">Final managed target path.</param>
    /// <param name="originalFileName">Original media file name.</param>
    /// <param name="transcript">Provider transcript.</param>
    /// <returns>The chat-owned managed attachment.</returns>
    internal static async Task<ManagedTranscriptAttachment> CreateAtomicAsync(string targetPath, string originalFileName, string transcript)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await WriteMarkdownAsync(temporaryPath, originalFileName, transcript);
            File.Move(temporaryPath, targetPath);
            return FromPath(targetPath, originalFileName, isStaged: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>Deletes a file only when its canonical path has an exact managed structure.</summary>
    /// <param name="attachment">Candidate managed attachment.</param>
    /// <returns>Whether an owned file was deleted.</returns>
    public static bool TryDeleteOwnedFile(FileAttachment attachment)
    {
        if (attachment is not ManagedTranscriptAttachment managed || !File.Exists(managed.FilePath))
            return false;

        var fileInfo = new FileInfo(managed.FilePath);
        var fullFilePath = Path.GetFullPath(fileInfo.FullName);
        var fullDataRoot = Path.GetFullPath(SettingsManager.DataDirectory!);
        var relative = Path.GetRelativePath(fullDataRoot, fullFilePath);
        
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison))
            return false;
        
        if (fileInfo.LinkTarget is not null || HasLinkedDirectory(fileInfo.Directory, fullDataRoot))
            return false;

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var isStaging = segments is ["media-staging", _, _]
                        && Guid.TryParseExact(segments[1], "N", out _)
                        && !string.IsNullOrWhiteSpace(segments[2]);
        
        var isTemporaryChatTranscript = segments is ["tempChats", _, _, _, _]
                                        && Guid.TryParse(segments[1], out _)
                                        && segments[2] == "attachments"
                                        && segments[3] == "transcripts";
        
        var isWorkspaceChatTranscript = segments is ["workspaces", _, _, _, _, _]
                                        && Guid.TryParse(segments[1], out _)
                                        && Guid.TryParse(segments[2], out _)
                                        && segments[3] == "attachments"
                                        && segments[4] == "transcripts";
        
        if (!isStaging && !isTemporaryChatTranscript && !isWorkspaceChatTranscript)
            return false;

        File.Delete(fullFilePath);
        var parent = Path.GetDirectoryName(fullFilePath);
        if (isStaging && parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
        
        return true;
    }

    /// <summary>Rejects paths traversing any symbolic-link or junction directory below the data root.</summary>
    private static bool HasLinkedDirectory(DirectoryInfo? directory, string fullDataRoot)
    {
        while (directory is not null && !string.Equals(Path.GetFullPath(directory.FullName), fullDataRoot, PathComparison))
        {
            if (directory.LinkTarget is not null)
                return true;
            
            directory = directory.Parent;
        }
        
        return directory is null;
    }

    /// <summary>Normalizes an original stem using Unicode scalar values and cross-platform rules.</summary>
    /// <param name="originalFileName">Original media file name.</param>
    /// <returns>A non-empty stem containing at most 80 Unicode text characters.</returns>
    internal static string NormalizeOriginalStem(string originalFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(originalFileName).Normalize(NormalizationForm.FormC);
        var normalized = new StringBuilder();
        
        var textCharacters = 0;
        foreach (var rune in stem.EnumerateRunes())
        {
            if (textCharacters == 80)
                break;
            
            var replacement = Rune.IsControl(rune) || rune.Value is '/' or '\\' or '<' or '>' or ':' or '"' or '|' or '?' or '*'
                ? new Rune('-')
                : rune;
            
            normalized.Append(replacement);
            textCharacters++;
        }

        var result = normalized.ToString().Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(result) ? "media" : result;
    }

    /// <summary>Creates an attachment record from a file already written to disk.</summary>
    private static ManagedTranscriptAttachment FromPath(string path, string originalFileName, bool isStaged) => new(
        Path.GetFileName(path),
        path,
        new FileInfo(path).Length,
        originalFileName,
        isStaged);

    /// <summary>Writes localized transcript Markdown without a UTF-8 byte-order mark.</summary>
    private static async Task WriteMarkdownAsync(string path, string originalFileName, string transcript)
    {
        var markdown = $"""
                        # Transcription: {originalFileName}
                        
                        {transcript.Trim()}
                        """;
        
        await File.WriteAllTextAsync(path, markdown, new UTF8Encoding(false));
    }

    /// <summary>Gets the platform path comparison used for canonical containment checks.</summary>
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}