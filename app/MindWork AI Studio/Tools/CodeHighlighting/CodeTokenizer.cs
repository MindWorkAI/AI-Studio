using System.Buffers;
using System.Collections.Frozen;

namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// Finds the parts of a code block which get a color, following the rules of its language.
/// </summary>
/// <remarks>
/// The chat highlights a code block anew with every chunk of a streamed answer, and AI Studio runs on
/// small computers such as a Raspberry Pi, too. Hence, the tokenizer reads the code once from its start
/// to its end, without regular expressions, and looks words up without a string for each of them. It
/// never fails: what the rules do not know stays plain, and a string or comment which is still open,
/// because the model has not written its end yet, runs to the end of its line or of the code.
/// </remarks>
public sealed class CodeTokenizer
{
    /// <summary>
    /// The characters which make a variable of their own right after its prefix, such as $? in a shell.
    /// </summary>
    private const string SPECIAL_VARIABLE_NAMES = "?#@*!$-";

    private static readonly SearchValues<char> HEX_DIGITS = SearchValues.Create("0123456789abcdefABCDEF");

    private readonly string code;
    private readonly CodeLanguageRules rules;
    private readonly List<CodeToken> tokens = [];

    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> keywords;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> types;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> literals;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> functionDeclarations;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> typeDeclarations;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> stringPrefixes;
    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> hyphenOperators;

    private int position;

    /// <summary>
    /// What the next word names, after a keyword such as def or class.
    /// </summary>
    private CodeTokenKind? expectedName;

    /// <summary>
    /// How deep the blocks of a style sheet nest at the current position.
    /// </summary>
    private int blockDepth;

    private CodeTokenizer(string code, CodeLanguageRules rules)
    {
        this.code = code;
        this.rules = rules;
        this.keywords = rules.Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        this.types = rules.Types.GetAlternateLookup<ReadOnlySpan<char>>();
        this.literals = rules.Literals.GetAlternateLookup<ReadOnlySpan<char>>();
        this.functionDeclarations = rules.FunctionDeclarations.GetAlternateLookup<ReadOnlySpan<char>>();
        this.typeDeclarations = rules.TypeDeclarations.GetAlternateLookup<ReadOnlySpan<char>>();
        this.stringPrefixes = rules.StringPrefixes.GetAlternateLookup<ReadOnlySpan<char>>();
        this.hyphenOperators = rules.HyphenOperators.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// Finds the parts of the code which get a color.
    /// </summary>
    /// <param name="code">The code of the block.</param>
    /// <param name="rules">The rules of its language.</param>
    /// <returns>The parts in the order of the code, none of them overlapping another. The code between
    /// them stays plain.</returns>
    public static IReadOnlyList<CodeToken> Tokenize(string code, CodeLanguageRules rules)
    {
        var tokenizer = new CodeTokenizer(code, rules);
        tokenizer.Run();
        return tokenizer.tokens;
    }

    private void Run()
    {
        while (this.position < this.code.Length)
        {
            if (char.IsWhiteSpace(this.code[this.position]))
            {
                this.position++;
                continue;
            }

            if (this.TryReadComment() || this.TryReadWord())
                continue;

            //
            // Anything else, such as a string or a parenthesis, ends what a keyword like def expected:
            //
            this.expectedName = null;
            if (this.TryReadPreprocessorDirective() || this.TryReadString() || this.TryReadQuotedName() || this.TryReadVariable()
                || this.TryReadAnnotation() || this.TryReadStyleSheetPart() || this.TryReadHyphenWord() || this.TryReadBracketType()
                || this.TryReadNumber())
                continue;

            this.position++;
        }
    }

    private bool TryReadComment()
    {
        if (this.rules.HasLongBrackets && this.StartsWithAt(this.position, "--") && this.TryFindLongBracketEnd(this.position + 2, out var longEnd))
        {
            this.AddAndMove(this.position, longEnd, CodeTokenKind.COMMENT);
            return true;
        }

        foreach (var comment in this.rules.BlockComments)
        {
            if (!this.StartsWithAt(this.position, comment.Open))
                continue;

            var close = this.code.IndexOf(comment.Close, this.position + comment.Open.Length, StringComparison.Ordinal);
            this.AddAndMove(this.position, close < 0 ? this.code.Length : close + comment.Close.Length, CodeTokenKind.COMMENT);
            return true;
        }

        foreach (var comment in this.rules.LineComments)
        {
            if (!this.StartsWithAt(this.position, comment))
                continue;

            if (this.rules.LineCommentsNeedSeparator && this.position > 0 && !IsSeparator(this.code[this.position - 1]))
                continue;

            this.AddAndMove(this.position, this.LineEnd(this.position), CodeTokenKind.COMMENT);
            return true;
        }

        return false;
    }

    private bool TryReadWord()
    {
        var start = this.position;
        if (!this.IsWordStart(start))
            return false;

        var end = this.WordEnd(start + 1);
        var word = this.code.AsSpan(start, end - start);

        //
        // A prefix, such as the f of an f-string, belongs to the string right behind it:
        //
        if (this.stringPrefixes.Contains(word) && this.TryReadStringAt(end, start))
        {
            this.expectedName = null;
            return true;
        }

        var expected = this.expectedName;
        this.expectedName = null;
        if (this.keywords.Contains(word))
        {
            if (this.functionDeclarations.Contains(word))
                this.expectedName = CodeTokenKind.FUNCTION;
            else if (this.typeDeclarations.Contains(word))
                this.expectedName = CodeTokenKind.TYPE;

            this.AddAndMove(start, end, CodeTokenKind.KEYWORD);
            return true;
        }

        if (this.ClassifyName(word, end, expected) is { } kind)
            this.AddAndMove(start, end, kind);
        else
            this.position = end;

        return true;
    }

    private CodeTokenKind? ClassifyName(ReadOnlySpan<char> word, int end, CodeTokenKind? expected)
    {
        if (this.literals.Contains(word))
            return CodeTokenKind.LITERAL;

        //
        // In function M.greet, the function is greet, and M is the table or object it belongs to. The
        // parenthesis after greet makes it a function then:
        //
        if (expected is CodeTokenKind.FUNCTION && end < this.code.Length && this.code[end] is '.' or ':')
            return null;

        if (expected is not null)
            return expected;

        if (this.types.Contains(word))
            return CodeTokenKind.TYPE;

        if (this.rules.IsStyleSheet && this.blockDepth > 0 && this.IsPropertyName(end))
            return CodeTokenKind.ATTRIBUTE;

        if (this.rules.HighlightFunctionCalls && end < this.code.Length && this.code[end] == '(')
            return CodeTokenKind.FUNCTION;

        if (this.rules.HighlightCommandNames && IsCommandName(word))
            return CodeTokenKind.FUNCTION;

        if (this.rules.HighlightPascalCaseTypes && IsPascalCase(word))
            return CodeTokenKind.TYPE;

        return null;
    }

    private bool TryReadPreprocessorDirective()
    {
        if (!this.rules.HasPreprocessor || this.code[this.position] != '#' || !this.IsAtLineStart(this.position))
            return false;

        this.AddAndMove(this.position, this.LineEnd(this.position), CodeTokenKind.META);
        return true;
    }

    private bool TryReadString()
    {
        if (this.TryReadStringAt(this.position, this.position))
            return true;

        if (!this.rules.HasLongBrackets || !this.TryFindLongBracketEnd(this.position, out var end))
            return false;

        this.AddAndMove(this.position, end, CodeTokenKind.STRING);
        return true;
    }

    /// <summary>
    /// Reads a string which opens at a position, perhaps behind a prefix.
    /// </summary>
    /// <param name="openAt">Where the string would open.</param>
    /// <param name="tokenStart">Where its part starts: at its prefix, when it has one.</param>
    /// <returns>Whether a string opens there.</returns>
    private bool TryReadStringAt(int openAt, int tokenStart)
    {
        foreach (var rule in this.rules.Strings)
        {
            if (!this.StartsWithAt(openAt, rule.Open))
                continue;

            this.AddAndMove(tokenStart, this.FindStringEnd(openAt + rule.Open.Length, rule), CodeTokenKind.STRING);
            return true;
        }

        return false;
    }

    private bool TryReadQuotedName()
    {
        foreach (var rule in this.rules.QuotedNames)
        {
            if (!this.StartsWithAt(this.position, rule.Open))
                continue;

            this.position = this.FindStringEnd(this.position + rule.Open.Length, rule);
            return true;
        }

        return false;
    }

    private int FindStringEnd(int from, CodeStringRule rule)
    {
        var index = from;
        while (index < this.code.Length)
        {
            var character = this.code[index];
            if (character == rule.Escape)
            {
                index += 2;
                continue;
            }

            if (!rule.IsMultiline && character is '\n')
                return this.LineEnd(index);

            if (this.StartsWithAt(index, rule.Close))
            {
                if (rule.DoubledCloseIsLiteral && this.StartsWithAt(index + rule.Close.Length, rule.Close))
                {
                    index += 2 * rule.Close.Length;
                    continue;
                }

                return index + rule.Close.Length;
            }

            index++;
        }

        return this.code.Length;
    }

    /// <summary>
    /// Finds the end of a long bracket of Lua, such as [[ … ]] or [==[ … ]==], which opens at a position.
    /// </summary>
    /// <param name="openAt">Where the bracket would open.</param>
    /// <param name="end">Where it ends, which is the end of the code when it was never closed.</param>
    /// <returns>Whether a long bracket opens there.</returns>
    private bool TryFindLongBracketEnd(int openAt, out int end)
    {
        end = openAt;
        if (openAt >= this.code.Length || this.code[openAt] != '[')
            return false;

        var level = 0;
        while (openAt + 1 + level < this.code.Length && this.code[openAt + 1 + level] == '=')
            level++;

        var contentStart = openAt + 2 + level;
        if (contentStart > this.code.Length || this.code[contentStart - 1] != '[')
            return false;

        var close = $"]{new string('=', level)}]";
        var closeAt = this.code.IndexOf(close, contentStart, StringComparison.Ordinal);
        end = closeAt < 0 ? this.code.Length : closeAt + close.Length;
        return true;
    }

    private bool TryReadVariable()
    {
        if (this.rules.VariablePrefix is not { } prefix || this.code[this.position] != prefix || this.position + 1 >= this.code.Length)
            return false;

        var next = this.code[this.position + 1];
        int end;
        if (next is '{')
        {
            var close = this.code.IndexOf('}', this.position + 2);
            var lineEnd = this.LineEnd(this.position);
            end = close < 0 || close > lineEnd ? lineEnd : close + 1;
        }
        else if (char.IsLetter(next) || next is '_')
        {
            end = this.position + 2;
            while (end < this.code.Length && (char.IsLetterOrDigit(this.code[end]) || this.code[end] is '_' || this.rules.VariableNameCharacters.Contains(this.code[end])))
                end++;
        }
        else if (char.IsAsciiDigit(next) || SPECIAL_VARIABLE_NAMES.Contains(next))
            end = this.position + 2;
        else
            return false;

        this.AddAndMove(this.position, end, CodeTokenKind.VARIABLE);
        return true;
    }

    private bool TryReadAnnotation()
    {
        if (this.rules.AnnotationPrefix is not { } prefix || this.code[this.position] != prefix || this.position + 1 >= this.code.Length)
            return false;

        var next = this.code[this.position + 1];
        if (!char.IsLetter(next) && next is not '_')
            return false;

        var end = this.position + 2;
        while (end < this.code.Length && (char.IsLetterOrDigit(this.code[end]) || this.code[end] is '_' or '.'))
            end++;

        this.AddAndMove(this.position, end, CodeTokenKind.META);
        return true;
    }

    private bool TryReadStyleSheetPart()
    {
        if (!this.rules.IsStyleSheet)
            return false;

        switch (this.code[this.position])
        {
            case '{':
                this.blockDepth++;
                this.position++;
                return true;

            case '}':
                this.blockDepth = Math.Max(0, this.blockDepth - 1);
                this.position++;
                return true;

            case '.' when this.IsWordStart(this.position + 1):
                this.AddAndMove(this.position, this.WordEnd(this.position + 1), CodeTokenKind.SELECTOR);
                return true;

            case '@' when this.IsWordStart(this.position + 1):
                this.AddAndMove(this.position, this.WordEnd(this.position + 1), CodeTokenKind.KEYWORD);
                return true;

            case '!' when this.StartsWithAt(this.position + 1, "important"):
                this.AddAndMove(this.position, this.position + 1 + "important".Length, CodeTokenKind.KEYWORD);
                return true;

            //
            // Within a block, #fff is a color. Before it, #main is the ID a selector names:
            //
            case '#' when this.position + 1 < this.code.Length && this.IsWordCharacter(this.code[this.position + 1]):
                var end = this.WordEnd(this.position + 1);
                var isColor = this.blockDepth > 0 && IsHexColor(this.code.AsSpan(this.position + 1, end - this.position - 1));
                this.AddAndMove(this.position, end, isColor ? CodeTokenKind.NUMBER : CodeTokenKind.SELECTOR);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Reads a word after a hyphen, such as -eq or -Path in PowerShell. An operator gets a color, the
    /// name of a parameter stays plain.
    /// </summary>
    private bool TryReadHyphenWord()
    {
        if (this.rules.HyphenOperators.Count is 0 || this.code[this.position] != '-' || this.position + 1 >= this.code.Length || !char.IsLetter(this.code[this.position + 1]))
            return false;

        if (this.position > 0 && this.IsWordCharacter(this.code[this.position - 1]))
            return false;

        var end = this.position + 2;
        while (end < this.code.Length && char.IsLetterOrDigit(this.code[end]))
            end++;

        if (this.hyphenOperators.Contains(this.code.AsSpan(this.position + 1, end - this.position - 1)))
            this.AddAndMove(this.position, end, CodeTokenKind.KEYWORD);
        else
            this.position = end;

        return true;
    }

    /// <summary>
    /// Reads a type in square brackets, such as [string] or [int[]] in PowerShell. Brackets right after
    /// a name or a closing bracket index into it instead, as in $list[0].
    /// </summary>
    private bool TryReadBracketType()
    {
        if (!this.rules.HasBracketTypes || this.code[this.position] != '[' || this.position + 1 >= this.code.Length || !char.IsLetter(this.code[this.position + 1]))
            return false;

        if (this.position > 0 && (this.IsWordCharacter(this.code[this.position - 1]) || this.code[this.position - 1] is ']' or ')'))
            return false;

        var end = this.position + 2;
        while (end < this.code.Length && (char.IsLetterOrDigit(this.code[end]) || this.code[end] is '.' or '_'))
            end++;

        if (this.StartsWithAt(end, "[]"))
            end += 2;

        if (end >= this.code.Length || this.code[end] != ']')
            return false;

        this.tokens.Add(new(this.position + 1, end - this.position - 1, CodeTokenKind.TYPE));
        this.position = end + 1;
        return true;
    }

    private bool TryReadNumber()
    {
        var start = this.position;
        var character = this.code[start];
        var startsWithPoint = character is '.' && start + 1 < this.code.Length && char.IsAsciiDigit(this.code[start + 1]) && (start is 0 || this.code[start - 1] != '.');
        if (!char.IsAsciiDigit(character) && !startsWithPoint)
            return false;

        int end;
        if (startsWithPoint)
            end = this.DigitsEnd(start + 1);
        else if (character is '0' && start + 1 < this.code.Length && this.code[start + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            end = start + 2;
            while (end < this.code.Length && (char.IsAsciiHexDigit(this.code[end]) || this.code[end] is '_'))
                end++;
        }
        else
        {
            end = this.DigitsEnd(start);
            if (end + 1 < this.code.Length && this.code[end] is '.' && char.IsAsciiDigit(this.code[end + 1]))
                end = this.DigitsEnd(end + 1);
        }

        if (end < this.code.Length && this.code[end] is 'e' or 'E')
        {
            var exponent = end + 1;
            if (exponent < this.code.Length && this.code[exponent] is '+' or '-')
                exponent++;

            if (exponent < this.code.Length && char.IsAsciiDigit(this.code[exponent]))
                end = this.DigitsEnd(exponent);
        }

        //
        // A suffix, such as the f of 1.5f, the UL of 10UL, or the px of 10px, belongs to the number:
        //
        while (end < this.code.Length && char.IsAsciiLetterOrDigit(this.code[end]))
            end++;

        if (this.rules.IsStyleSheet && end < this.code.Length && this.code[end] is '%')
            end++;

        this.AddAndMove(start, end, CodeTokenKind.NUMBER);
        return true;
    }

    /// <summary>
    /// Whether a word inside a block of a style sheet names a property: a colon follows it, and its
    /// line goes on to a semicolon or to the end of the block, not to a block of its own, as the
    /// selector a:hover { does.
    /// </summary>
    private bool IsPropertyName(int end)
    {
        var index = end;
        while (index < this.code.Length && this.code[index] is ' ' or '\t')
            index++;

        if (index >= this.code.Length || this.code[index] != ':')
            return false;

        var lineEnd = this.LineEnd(index);
        for (var next = index + 1; next < lineEnd; next++)
        {
            switch (this.code[next])
            {
                case '{':
                    return false;

                case ';' or '}':
                    return true;
            }
        }

        return true;
    }

    private bool IsWordStart(int index)
    {
        if (index >= this.code.Length)
            return false;

        var character = this.code[index];
        if (char.IsLetter(character) || character is '_')
            return true;

        if (!this.rules.WordStartCharacters.Contains(character))
            return false;

        //
        // A hyphen alone is a minus sign, as in 10px - 2px or -2px:
        //
        if (character is not '-')
            return true;

        return index + 1 < this.code.Length && (char.IsLetter(this.code[index + 1]) || this.code[index + 1] is '_' or '-');
    }

    private bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || character is '_' || this.rules.WordCharacters.Contains(character);

    private int WordEnd(int from)
    {
        var end = from;
        while (end < this.code.Length && this.IsWordCharacter(this.code[end]))
            end++;

        return end;
    }

    private int DigitsEnd(int from)
    {
        var end = from;
        while (end < this.code.Length && (char.IsAsciiDigit(this.code[end]) || this.code[end] is '_'))
            end++;

        return end;
    }

    /// <summary>
    /// Where the line of a position ends, before its line break.
    /// </summary>
    private int LineEnd(int from)
    {
        var lineBreak = this.code.IndexOf('\n', from);
        if (lineBreak < 0)
            return this.code.Length;

        return lineBreak > from && this.code[lineBreak - 1] is '\r' ? lineBreak - 1 : lineBreak;
    }

    private bool IsAtLineStart(int index)
    {
        for (var previous = index - 1; previous >= 0; previous--)
        {
            switch (this.code[previous])
            {
                case '\n':
                    return true;

                case not (' ' or '\t'):
                    return false;
            }
        }

        return true;
    }

    private bool StartsWithAt(int index, string value) => index <= this.code.Length && this.code.AsSpan(index).StartsWith(value, StringComparison.Ordinal);

    private void AddAndMove(int start, int end, CodeTokenKind kind)
    {
        this.tokens.Add(new(start, end - start, kind));
        this.position = end;
    }

    private static bool IsSeparator(char character) => char.IsWhiteSpace(character) || character is ';' or '(' or ')' or '{' or '}' or '|' or '&';

    private static bool IsPascalCase(ReadOnlySpan<char> word) => word.Length > 1 && char.IsUpper(word[0]) && word.ContainsAnyInRange('a', 'z');

    /// <summary>
    /// Whether a word has the form Verb-Noun, as the commands of PowerShell do.
    /// </summary>
    private static bool IsCommandName(ReadOnlySpan<char> word)
    {
        var hyphen = word.IndexOf('-');
        return hyphen > 0 && hyphen < word.Length - 1 && char.IsLetter(word[hyphen - 1]) && char.IsLetter(word[hyphen + 1]);
    }

    private static bool IsHexColor(ReadOnlySpan<char> value) => value.Length is 3 or 4 or 6 or 8 && !value.ContainsAnyExcept(HEX_DIGITS);
}