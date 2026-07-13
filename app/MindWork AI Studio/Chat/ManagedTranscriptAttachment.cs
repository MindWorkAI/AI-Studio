using System.Text;

using AIStudio.Settings;

namespace AIStudio.Chat;

public sealed record ManagedTranscriptAttachment(string FileName, string FilePath, long FileSizeBytes, string OriginalFileName, bool IsStaged)
    : FileAttachment(FileAttachmentType.DOCUMENT, FileName, FilePath, FileSizeBytes)
{
    public override FileAttachment Normalize()
    {
        var size = File.Exists(this.FilePath) ? new FileInfo(this.FilePath).Length : 0;
        return this with { FileName = Path.GetFileName(this.FilePath), FileSizeBytes = size };
    }

    public static async Task<ManagedTranscriptAttachment> CreateStagedAsync(string originalPath, string transcript)
    {
        var operationDirectory = Path.Combine(SettingsManager.DataDirectory!, "media-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationDirectory);
        
        var originalFileName = Path.GetFileName(originalPath);
        var stagingPath = Path.Combine(operationDirectory, $"{Guid.NewGuid():N}.md");
        var markdown = $"""
                        # Transcription of {originalFileName}
                        
                        {transcript.Trim()}
                        """;
        
        await File.WriteAllTextAsync(stagingPath, markdown, new UTF8Encoding(false));
        return new ManagedTranscriptAttachment(
            Path.GetFileName(stagingPath),
            stagingPath,
            new FileInfo(stagingPath).Length,
            originalFileName,
            true);
    }

    public static bool TryDeleteOwnedFile(FileAttachment attachment)
    {
        if (attachment is not ManagedTranscriptAttachment managed || !File.Exists(managed.FilePath))
            return false;

        var fileInfo = new FileInfo(managed.FilePath);
        var parentInfo = fileInfo.Directory!;
        var canonicalParent = parentInfo.ResolveLinkTarget(true)?.FullName ?? parentInfo.FullName;
        var fullPath = fileInfo.ResolveLinkTarget(true)?.FullName ?? Path.Combine(canonicalParent, fileInfo.Name);
        var dataDirectory = new DirectoryInfo(SettingsManager.DataDirectory!);
        
        var canonicalDataRoot = EnsureTrailingSeparator(dataDirectory.ResolveLinkTarget(true)?.FullName ?? dataDirectory.FullName);
        var stagingDirectory = new DirectoryInfo(Path.Combine(SettingsManager.DataDirectory!, "media-staging"));
        var stagingRoot = EnsureTrailingSeparator(stagingDirectory.ResolveLinkTarget(true)?.FullName ?? stagingDirectory.FullName);
        
        var transcriptsSegment = $"{Path.DirectorySeparatorChar}attachments{Path.DirectorySeparatorChar}transcripts{Path.DirectorySeparatorChar}";
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var owned = fullPath.StartsWith(stagingRoot, comparison)
                    || (fullPath.StartsWith(canonicalDataRoot, comparison)
                        && fullPath.Contains(transcriptsSegment, comparison));
        
        if (!owned)
            return false;

        File.Delete(fullPath);
        var parent = Path.GetDirectoryName(fullPath);
        if (managed.IsStaged && parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);
        
        return true;
    }

    internal static string NormalizeOriginalStem(string originalFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(originalFileName).Normalize(NormalizationForm.FormC);
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var normalized = new StringBuilder();
        
        foreach (var character in stem)
        {
            normalized.Append(char.IsControl(character)
                              || invalid.Contains(character)
                              || character is '/' or '\\'
                ? '-'
                : character);
        }
        
        var result = normalized.ToString().Trim(' ', '.', '-');
        if (string.IsNullOrWhiteSpace(result))
            result = "media";
        
        return result.Length <= 80 ? result : result[..80];
    }

    private static string EnsureTrailingSeparator(string path) => path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}