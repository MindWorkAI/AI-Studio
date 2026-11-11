using System.Collections.Frozen;

namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// How a language looks to the CodeTokenizer: its words, comments, strings, and the few habits of its
/// own, such as the dollar sign of a shell variable.
/// </summary>
/// <remarks>
/// The rules describe what a reader recognizes at a glance, not the grammar of the language. Whatever
/// they leave out stays plain, which is no error.
/// </remarks>
public sealed class CodeLanguageRules
{
    private static readonly FrozenSet<string> NO_WORDS = Words();

    /// <summary>
    /// The keywords, such as if or return.
    /// </summary>
    public FrozenSet<string> Keywords { get; init; } = NO_WORDS;

    /// <summary>
    /// The names of built-in types, such as int.
    /// </summary>
    public FrozenSet<string> Types { get; init; } = NO_WORDS;

    /// <summary>
    /// The words which stand for a value, such as true or null.
    /// </summary>
    public FrozenSet<string> Literals { get; init; } = NO_WORDS;

    /// <summary>
    /// The keywords after which the name of a function follows, such as def. Each of them has to be one
    /// of the keywords, too.
    /// </summary>
    public FrozenSet<string> FunctionDeclarations { get; init; } = NO_WORDS;

    /// <summary>
    /// The keywords after which the name of a type follows, such as class. Each of them has to be one of
    /// the keywords, too.
    /// </summary>
    public FrozenSet<string> TypeDeclarations { get; init; } = NO_WORDS;

    /// <summary>
    /// What starts a comment to the end of its line, such as //.
    /// </summary>
    public IReadOnlyList<string> LineComments { get; init; } = [];

    /// <summary>
    /// Whether a line comment starts only at the beginning of a word, as the # of a shell does: in $# or
    /// a#b, it starts none.
    /// </summary>
    public bool LineCommentsNeedSeparator { get; init; }

    /// <summary>
    /// The kinds of block comments, such as /* … */.
    /// </summary>
    public IReadOnlyList<CodeDelimiters> BlockComments { get; init; } = [];

    /// <summary>
    /// The kinds of strings. The first one whose opening matches wins, so a longer opening, such as three
    /// quotation marks, has to come before a shorter one.
    /// </summary>
    public IReadOnlyList<CodeStringRule> Strings { get; init; } = [];

    /// <summary>
    /// The kinds of quoted names, such as "column" in SQL. They stay plain, but no keyword within them
    /// gets a color.
    /// </summary>
    public IReadOnlyList<CodeStringRule> QuotedNames { get; init; } = [];

    /// <summary>
    /// The words which may stand right before a string and belong to it, such as the f of an f-string in
    /// Python.
    /// </summary>
    public FrozenSet<string> StringPrefixes { get; init; } = NO_WORDS;

    /// <summary>
    /// Whether the language writes long strings and comments in brackets, as Lua does with [[ … ]] and
    /// --[==[ … ]==].
    /// </summary>
    public bool HasLongBrackets { get; init; }

    /// <summary>
    /// The character which starts the name of a variable, such as $, or null when there is none.
    /// </summary>
    public char? VariablePrefix { get; init; }

    /// <summary>
    /// The characters which may appear within the name of a variable beyond letters, digits, and the
    /// underscore, such as the colon of $env:PATH.
    /// </summary>
    public string VariableNameCharacters { get; init; } = string.Empty;

    /// <summary>
    /// The character which starts an annotation or decorator, such as @, or null when there is none.
    /// </summary>
    public char? AnnotationPrefix { get; init; }

    /// <summary>
    /// Whether a # at the start of a line begins a directive to the end of the line, as #include does.
    /// </summary>
    public bool HasPreprocessor { get; init; }

    /// <summary>
    /// The characters which may start a word beyond letters and the underscore, such as the $ of
    /// JavaScript. A hyphen starts a word only when a letter, an underscore, or another hyphen follows it.
    /// </summary>
    public string WordStartCharacters { get; init; } = string.Empty;

    /// <summary>
    /// The characters which may appear within a word beyond letters, digits, and the underscore, such as
    /// the hyphen of a CSS property.
    /// </summary>
    public string WordCharacters { get; init; } = string.Empty;

    /// <summary>
    /// Whether a name followed directly by an opening parenthesis is a function.
    /// </summary>
    public bool HighlightFunctionCalls { get; init; } = true;

    /// <summary>
    /// Whether a name in PascalCase is a type, as in Java.
    /// </summary>
    public bool HighlightPascalCaseTypes { get; init; }

    /// <summary>
    /// Whether a word of the form Verb-Noun is a command, as in PowerShell.
    /// </summary>
    public bool HighlightCommandNames { get; init; }

    /// <summary>
    /// The operators written as a word after a hyphen, such as -eq in PowerShell. Any other word after a
    /// hyphen is the name of a parameter and stays plain.
    /// </summary>
    public FrozenSet<string> HyphenOperators { get; init; } = NO_WORDS;

    /// <summary>
    /// Whether a name in square brackets is a type, as [string] in PowerShell.
    /// </summary>
    public bool HasBracketTypes { get; init; }

    /// <summary>
    /// Whether the code is a style sheet: selectors, properties, and values instead of statements.
    /// </summary>
    public bool IsStyleSheet { get; init; }

    /// <summary>
    /// A set of words which match exactly as they are written.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <returns>The set of words.</returns>
    public static FrozenSet<string> Words(params string[] words) => words.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// A set of words which match in any case, for the languages which ignore it, such as SQL.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <returns>The set of words.</returns>
    public static FrozenSet<string> WordsInAnyCase(params string[] words) => words.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}