namespace AIStudio.Assistants.BatchProcessing;

/// <summary>
/// One entry of the first level of the input folder, which a batch run hands to the model in one request.
/// </summary>
/// <remarks>
/// A file directly in the input folder is a unit of its own. A subfolder is one unit as well: all of its
/// selected files, from every level below it, are merged and sent to the model together. This way, a
/// folder per case, per customer, or per project gets one answer instead of one per file.
/// </remarks>
/// <param name="Path">The full path of the file or the subfolder.</param>
/// <param name="RelativePath">The path relative to the input folder, i.e., the name of the file or subfolder.</param>
/// <param name="MemberFiles">The selected files of a subfolder, sorted by path; empty for a file.</param>
internal sealed record BatchProcessingUnit(string Path, string RelativePath, IReadOnlyList<string> MemberFiles)
{
    /// <summary>
    /// Whether this unit is a subfolder whose files are merged.
    /// </summary>
    public bool IsFolder => this.MemberFiles.Count > 0;

    /// <summary>
    /// Every file the unit consists of: the file itself, or the files of the subfolder.
    /// </summary>
    public IReadOnlyList<string> Files => this.IsFolder ? this.MemberFiles : [this.Path];

    /// <summary>
    /// Groups the selected files of a batch run by the entry of the first level they belong to.
    /// </summary>
    /// <param name="inputDirectory">The input folder.</param>
    /// <param name="files">The full paths of the selected files.</param>
    /// <returns>The units, sorted by their relative path.</returns>
    public static IReadOnlyList<BatchProcessingUnit> Group(string inputDirectory, IEnumerable<string> files)
    {
        var units = new SortedDictionary<string, (string Path, SortedSet<string> Members)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var relativePath = System.IO.Path.GetRelativePath(inputDirectory, file);
            var separatorIndex = relativePath.IndexOfAny([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar]);
            if (separatorIndex < 0)
            {
                units[relativePath] = (file, new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            var folderName = relativePath[..separatorIndex];
            if (!units.TryGetValue(folderName, out var folder))
            {
                folder = (System.IO.Path.Join(inputDirectory, folderName), new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                units[folderName] = folder;
            }

            folder.Members.Add(file);
        }

        return [..units.Select(unit => new BatchProcessingUnit(unit.Value.Path, unit.Key, [..unit.Value.Members]))];
    }
}