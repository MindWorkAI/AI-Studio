using System.IO.Enumeration;

using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// The files and folders which folder-based features leave out, whatever their file patterns
/// select: the metadata of operating systems, which is built in, and a list the user can change.
/// </summary>
/// <remarks>
/// An entry which ends with a slash names folders, e.g., bin/, wherever they are below the selected
/// folder. Unlike in .gitignore, an entry has no path, so /bin is no entry at all. Every other entry
/// is a file name pattern, e.g., *.min.js. Both ignore case and accept the wildcards * and ?.
/// Entries are separated by semicolons or line breaks.
///
/// Exclusion comes before selection: an excluded folder is not even entered, so node_modules costs
/// no time, and an excluded file stays out even when a file pattern selects it.
/// </remarks>
internal static class FileExclusions
{
    /// <summary>
    /// What an entry which names folders ends with.
    /// </summary>
    public const char FOLDER_SUFFIX = '/';

    //
    // Folders of dependencies and build outputs which are not hidden. Hidden ones, such as .git,
    // .venv, or .gradle, are left out by the rule for hidden files and folders anyway. Names which
    // are common in office work stay out of the list on purpose: vendor (suppliers), out,
    // coverage (press, insurance), and packages.
    //
    private const string DEPENDENCY_AND_BUILD_FOLDERS = "node_modules/; bower_components/; Pods/; venv/; env/; __pycache__/; bin/; obj/; Debug/; Release/; target/; build/; dist/; DerivedData/; cmake-build-*/; TestResults/; htmlcov/";

    //
    // Virtual machines are folders on macOS. Besides huge disk images, they hold text
    // configurations, e.g., .vmx or config.pvs, which the check for readable text would select:
    //
    private const string VIRTUAL_MACHINE_FOLDERS = "*.vmwarevm/; *.pvm/; *.utm/";

    private const string LOCK_FILES = "package-lock.json; yarn.lock; pnpm-lock.yaml; packages.lock.json; Cargo.lock; poetry.lock; Pipfile.lock; composer.lock; Gemfile.lock; go.sum";

    //
    // Only the source maps of scripts and styles: *.map alone would also hit the map files of
    // MapServer, which people write themselves:
    //
    private const string GENERATED_FILES = "*.min.js; *.min.css; *.js.map; *.css.map";

    //
    // The tokenizers of language models are JSON files of several megabytes, which :documents:
    // would select as configuration files. The entry also covers names with a prefix, such as
    // chat_tokenizer.json, but not the small tokenizer_config.json:
    //
    private const string TOKENIZER_FILES = "*tokenizer.json";

    //
    // Programs, databases, and disk images are binary, so :documents: never selects them. The list
    // names them anyway, so that * does not fill the embedding page with skipped files. SQLite
    // prescribes no extension, these are the common ones; -* covers its -wal, -shm, and -journal
    // companions:
    //
    private const string PROGRAM_FILES = "*.exe; *.dll; *.so; *.dylib; *.pdb";
    private const string DATABASE_FILES = "*.db; *.db3; *.db-*; *.sqlite; *.sqlite3; *.sqlite-*";
    private const string DISK_IMAGE_FILES = "*.vmdk; *.vdi; *.vhd; *.vhdx; *.qcow2; *.hdd; *.hds; *.iso; *.dmg; *.img";

    /// <summary>
    /// The list a data source or the batch processing assistant starts with. Its line breaks only
    /// group the entries for the reader of this code: the exclusion editor shows folders and files in
    /// fields of their own.
    /// </summary>
    public const string DEFAULT_PATTERNS =
        DEPENDENCY_AND_BUILD_FOLDERS + "\n" +
        VIRTUAL_MACHINE_FOLDERS + "\n" +
        LOCK_FILES + "\n" +
        GENERATED_FILES + "\n" +
        TOKENIZER_FILES + "\n" +
        PROGRAM_FILES + "\n" +
        DATABASE_FILES + "\n" +
        DISK_IMAGE_FILES;

    private const string OFFICE_LOCK_FILE_PATTERN = "~$*";

    /// <summary>
    /// Files nobody ever wanted as content: what operating systems leave in folders, and the lock
    /// files of Office. Windows marks some of them as system files, but a copy over a network
    /// drive, by a ZIP file, or onto a Mac loses that mark, and desktop.ini would then pass as a
    /// configuration file.
    /// </summary>
    private static readonly string[] ALWAYS_EXCLUDED_FILES = ["Thumbs.db", "ehthumbs.db", "ehthumbs_vista.db", "desktop.ini", ".DS_Store", "Icon\r", OFFICE_LOCK_FILE_PATTERN];

    /// <summary>
    /// Folders nobody ever wanted as content: what operating systems and ZIP files on macOS leave behind.
    /// </summary>
    private static readonly string[] ALWAYS_EXCLUDED_FOLDERS = ["__MACOSX", "$RECYCLE.BIN", "System Volume Information"];

    private static readonly char[] SEPARATORS = [';', '\r', '\n'];

    private enum ValidationIssue
    {
        NONE,
        SELECTOR,
        PATH,
        RECURSIVE_WILDCARD,
        INVALID_CHARACTER,
        FILE_IN_FOLDER_LIST,
        FOLDER_IN_FILE_LIST,
    }

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(FileExclusions).Namespace, nameof(FileExclusions));

    /// <summary>
    /// Splits a list into its entries. Empty lines and empty entries are allowed and dropped.
    /// </summary>
    public static string[] Parse(string? patterns) => (patterns ?? string.Empty)
        .Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static bool IsValid(string? patterns) => GetValidationIssue(patterns) is ValidationIssue.NONE;

    public static bool IsValidFolders(string? patterns) => GetValidationIssue(patterns, folders: true) is ValidationIssue.NONE;

    public static bool IsValidFiles(string? patterns) => GetValidationIssue(patterns, folders: false) is ValidationIssue.NONE;

    /// <summary>
    /// Validates a list of excluded files and folders. An empty list is valid: it excludes nothing
    /// beyond what is built in.
    /// </summary>
    public static string? Validate(string? patterns) => DescribeValidationIssue(GetValidationIssue(patterns));

    /// <summary>
    /// Validates the folder field without guessing whether an entry without a slash was meant as a folder.
    /// </summary>
    public static string? ValidateFolders(string? patterns) => DescribeValidationIssue(GetValidationIssue(patterns, folders: true));

    /// <summary>
    /// Validates the file field, directing folder rules to their own field.
    /// </summary>
    public static string? ValidateFiles(string? patterns) => DescribeValidationIssue(GetValidationIssue(patterns, folders: false));

    private static string? DescribeValidationIssue(ValidationIssue issue) => issue switch
    {
        ValidationIssue.NONE => null,
        ValidationIssue.SELECTOR => TB("Selectors such as :text: cannot be excluded. Please use file name patterns such as *.min.js or folder names such as bin/."),
        ValidationIssue.PATH => TB("Please write excluded folders as a name without a path, ending with a slash, e.g., node_modules/ or bin/. Such an entry excludes every folder of that name."),
        ValidationIssue.RECURSIVE_WILDCARD => TB("Please use only single asterisks as wildcards, e.g., *.min.js or cmake-build-*/."),
        ValidationIssue.INVALID_CHARACTER => TB("One of the excluded files or folders contains an invalid character."),
        ValidationIssue.FILE_IN_FOLDER_LIST => TB("Folder patterns must end with /, e.g., build/. Enter file patterns such as *.pdf in 'Files'."),
        ValidationIssue.FOLDER_IN_FILE_LIST => TB("Enter folder patterns ending with / in 'Folders'. File patterns must not end with /."),
        _ => throw new ArgumentOutOfRangeException(nameof(issue)),
    };

    /// <summary>
    /// Whether a folder is left out, either by the built-in list or by one of the entries.
    /// </summary>
    public static bool ExcludesFolder(string folderPath, IReadOnlyList<string> exclusions)
    {
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
        return IsAlwaysExcludedFolder(folderName)
               || exclusions.Any(entry => IsFolderEntry(entry) && FileSystemName.MatchesSimpleExpression(entry.AsSpan()[..^1], folderName, ignoreCase: true));
    }

    /// <summary>
    /// Whether a file is left out, either by the built-in list or by one of the entries.
    /// </summary>
    public static bool ExcludesFile(string filePath, IReadOnlyList<string> exclusions)
    {
        var fileName = Path.GetFileName(filePath);
        return IsAlwaysExcludedFile(fileName)
               || exclusions.Any(entry => !IsFolderEntry(entry) && FileSystemName.MatchesSimpleExpression(entry, fileName, ignoreCase: true));
    }

    /// <summary>
    /// Whether a file is one nobody ever wanted as content, whatever any list says.
    /// </summary>
    public static bool IsAlwaysExcludedFile(string fileName) =>
        FileTypes.IsAllowedPath(fileName, FileTypes.SHORTCUT)
        || ALWAYS_EXCLUDED_FILES.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName, ignoreCase: true));

    /// <summary>
    /// Whether a folder is one nobody ever wanted as content, whatever any list says.
    /// </summary>
    public static bool IsAlwaysExcludedFolder(string folderName) => ALWAYS_EXCLUDED_FOLDERS.Contains(folderName, StringComparer.OrdinalIgnoreCase);

    private static bool IsFolderEntry(string entry) => entry.EndsWith(FOLDER_SUFFIX);

    private static ValidationIssue GetValidationIssue(string? patterns, bool? folders = null)
    {
        foreach (var entry in Parse(patterns))
        {
            if (FileNamePatterns.LooksLikeSelector(entry))
                return ValidationIssue.SELECTOR;

            var name = IsFolderEntry(entry) ? entry[..^1] : entry;
            if (string.IsNullOrWhiteSpace(name)
                || name is "." or ".."
                || name.EndsWith("..", StringComparison.Ordinal)
                || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\']) >= 0)
                return ValidationIssue.PATH;

            if (name.Contains("**", StringComparison.Ordinal))
                return ValidationIssue.RECURSIVE_WILDCARD;

            if (name.IndexOfAny(FileNamePatterns.INVALID_PATTERN_CHARACTERS) >= 0)
                return ValidationIssue.INVALID_CHARACTER;

            if (folders is not null && IsFolderEntry(entry) != folders.Value)
                return folders.Value ? ValidationIssue.FILE_IN_FOLDER_LIST : ValidationIssue.FOLDER_IN_FILE_LIST;
        }

        return ValidationIssue.NONE;
    }
}