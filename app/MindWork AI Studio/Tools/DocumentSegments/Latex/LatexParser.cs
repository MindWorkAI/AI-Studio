namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// Reads the markup of a LaTeX document into nodes, with where each one lies in the source.
/// </summary>
/// <remarks>
/// <para>
/// The parser reads only as much as the segments need: it expands no macro and follows no change of category
/// codes. Which arguments a command takes comes from LatexCommands; an unknown command takes every argument which
/// follows right after it.
/// </para>
/// <para>
/// It never fails. Markup it cannot follow, e.g., a closing brace without an opening one or a formula without its
/// end, becomes an INVALID node, so that the reader leaves the paragraph around it as it is.
/// </para>
/// </remarks>
internal sealed class LatexParser
{
    /// <summary>
    /// The longest overlay specification of Beamer which may follow a command, e.g., &lt;2-&gt;.
    /// </summary>
    private const int MAX_OVERLAY_LENGTH = 40;

    /// <summary>
    /// The longest name of an environment.
    /// </summary>
    private const int MAX_ENVIRONMENT_NAME_LENGTH = 64;

    private readonly string text;
    private int position;

    private LatexParser(string text, int start)
    {
        this.text = text;
        this.position = start;
    }

    /// <summary>
    /// Reads a part of a document.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <param name="start">Where the part starts.</param>
    /// <param name="end">Where the part ends, exclusive.</param>
    /// <returns>The nodes, which cover the part without gaps.</returns>
    public static IReadOnlyList<LatexNode> Parse(string text, int start, int end) => new LatexParser(text, start).ParseSequence(end, null, null);

    /// <summary>
    /// Reads nodes until the end of what is read, a closing character, or the end of an environment.
    /// </summary>
    private List<LatexNode> ParseSequence(int limit, char? closing, string? environment)
    {
        var nodes = new List<LatexNode>();
        while (this.position < limit && this.text[this.position] != closing)
        {
            if (this.TryReadEnd(this.position, limit, out var name, out var end))
            {
                if (name == environment)
                    break;

                // The end of an environment which is not open here:
                nodes.Add(new(LatexNodeKind.INVALID, this.position, end));
                this.position = end;
                continue;
            }

            nodes.Add(this.ParseNode(limit, closing));
        }

        return nodes;
    }

    private LatexNode ParseNode(int limit, char? closing)
    {
        var start = this.position;
        var character = this.text[start];
        switch (character)
        {
            case '\\':
                return this.ParseControlSequence(limit);

            case '{':
                return this.ParseGroup(limit);

            case '%':
                return this.ParseComment(limit);

            case '$':
                return this.ParseDollarMath(limit);

            case '}':
                this.position++;
                return new(LatexNodeKind.INVALID, start, this.position);

            case '~' or '&' or '^' or '_' or '#':
                this.position++;
                return new(LatexNodeKind.SPECIAL, start, this.position) { Name = character.ToString() };

            case '\r' or '\n' when this.IsParagraphBreak(start, limit):
                this.SkipWhiteSpace(limit);
                return new(LatexNodeKind.PARAGRAPH_BREAK, start, this.position);

            default:
                this.SkipText(limit, closing);
                return new(LatexNodeKind.TEXT, start, this.position);
        }
    }

    private void SkipText(int limit, char? closing)
    {
        // The first character is words in any case, e.g., a closing bracket outside of an optional argument:
        this.position++;
        while (this.position < limit)
        {
            var character = this.text[this.position];
            if (IsSpecial(character) || character == closing || (character is '\r' or '\n' && this.IsParagraphBreak(this.position, limit)))
                return;

            this.position++;
        }
    }

    private static bool IsSpecial(char character) => character is '\\' or '{' or '}' or '%' or '$' or '~' or '&' or '^' or '_' or '#';

    private LatexNode ParseGroup(int limit)
    {
        var start = this.position;
        this.position++;
        var children = this.ParseSequence(limit, '}', null);
        if (this.position >= limit)
            return new(LatexNodeKind.INVALID, start, this.position);

        this.position++;
        return new(LatexNodeKind.GROUP, start, this.position) { Children = children };
    }

    private LatexNode ParseComment(int limit)
    {
        var start = this.position;
        this.SkipToLineEnd(limit);

        // As for TeX, the line end belongs to the comment, unless a blank line follows, which ends the paragraph:
        if (this.position < limit && !this.IsParagraphBreak(this.position, limit))
        {
            this.SkipLineEnd(limit);
            this.SkipSpaces(limit);
        }

        return new(LatexNodeKind.COMMENT, start, this.position);
    }

    private LatexNode ParseDollarMath(int limit)
    {
        var start = this.position;
        var terminator = start + 1 < limit && this.text[start + 1] == '$' ? "$$" : "$";
        this.position = start + terminator.Length;
        if (this.TrySkipMath(limit, terminator))
            return new(LatexNodeKind.MATH, start, this.position);

        this.position = start + 1;
        return new(LatexNodeKind.INVALID, start, this.position);
    }

    private LatexNode ParseMath(int start, string terminator, int limit)
    {
        if (this.TrySkipMath(limit, terminator))
            return new(LatexNodeKind.MATH, start, this.position);

        this.position = start + 2;
        return new(LatexNodeKind.INVALID, start, this.position);
    }

    /// <summary>
    /// Skips a formula up to and including its terminator, e.g., "$" or "\)".
    /// </summary>
    /// <returns>Whether the formula ends within its paragraph.</returns>
    private bool TrySkipMath(int limit, string terminator)
    {
        while (this.position < limit)
        {
            if (this.text.AsSpan(this.position, limit - this.position).StartsWith(terminator, StringComparison.Ordinal))
            {
                this.position += terminator.Length;
                return true;
            }

            switch (this.text[this.position])
            {
                case '\\':
                    this.position = Math.Min(this.position + 2, limit);
                    break;

                case '%':
                    this.SkipToLineEnd(limit);
                    break;

                case '\r' or '\n' when this.IsParagraphBreak(this.position, limit):
                    return false;

                default:
                    this.position++;
                    break;
            }
        }

        return false;
    }

    private LatexNode ParseControlSequence(int limit)
    {
        var start = this.position;
        if (start + 1 >= limit)
        {
            this.position = limit;
            return new(LatexNodeKind.INVALID, start, limit);
        }

        var next = this.text[start + 1];
        if (char.IsAsciiLetter(next))
        {
            var nameEnd = start + 1;
            while (nameEnd < limit && char.IsAsciiLetter(this.text[nameEnd]))
                nameEnd++;

            var name = this.text[(start + 1)..nameEnd];
            this.position = nameEnd;
            return name switch
            {
                "begin" => this.ParseEnvironment(start, limit),
                "end" => new(LatexNodeKind.INVALID, start, nameEnd),
                "verb" or "Verb" => this.ParseVerb(start, limit),
                "lstinline" or "mintinline" => this.ParseInlineCode(start, name, limit),
                "def" or "gdef" or "edef" or "xdef" => this.ParseDefinition(start, name, limit),

                _ => this.TryParseLetterWords(start, name, limit) ?? this.TryParseAccent(start, name, limit) ?? this.ParseCommand(start, name, limit),
            };
        }

        this.position = start + 2;
        switch (next)
        {
            case '(':
                return this.ParseMath(start, @"\)", limit);

            case '[':
                return this.ParseMath(start, @"\]", limit);

            case '%' or '&' or '$' or '#' or '_' or '{' or '}':
                return Escaped(start, this.position, next.ToString());

            case ' ' or '\t':
                return Escaped(start, this.position, " ");

            case '\r' or '\n':
                this.position = start + 1;
                this.SkipLineEnd(limit);
                return Escaped(start, this.position, " ");

            // A possible hyphenation, a correction of italics, or a hint for the space after a full stop:
            case '-' or '/' or '@':
                return Escaped(start, this.position, string.Empty);

            case ',':
                return Escaped(start, this.position, LatexWords.THIN_SPACE);
        }

        if (LatexWords.IsSymbolAccent(next))
            return this.TryParseAccent(start, next.ToString(), limit) ?? new LatexNode(LatexNodeKind.INVALID, start, this.position);

        return this.ParseCommand(start, next.ToString(), limit);
    }

    private static LatexNode Escaped(int start, int end, string words) => new(LatexNodeKind.ESCAPED_WORDS, start, end) { Words = words };

    /// <summary>
    /// Reads a command with letters which stands for a character, e.g., \ss.
    /// </summary>
    private LatexNode? TryParseLetterWords(int start, string name, int limit)
    {
        if (!LatexWords.TryLetterCommand(name, out var words))
            return null;

        this.SkipSpaceAfterControlWord(limit);
        return Escaped(start, this.position, words);
    }

    /// <summary>
    /// Reads an accent with its letter, e.g., \"a, \"{a}, or \c{c}.
    /// </summary>
    /// <returns>The letter with its accent, or null with the position unchanged when no letter follows.</returns>
    private LatexNode? TryParseAccent(int start, string accent, int limit)
    {
        if (!LatexWords.TryAccent(accent, out var combining))
            return null;

        var afterAccent = this.position;
        var isLetterAccent = char.IsAsciiLetter(accent[0]);
        if (isLetterAccent)
            this.SkipSpaces(limit);

        // An accent of letters takes a letter only after a space, since \cc would be a command of its own:
        if (this.TryReadAccentBase(limit, allowsBareLetter: !isLetterAccent || this.position > afterAccent, out var letter))
            return Escaped(start, this.position, LatexWords.WithAccent(letter, combining));

        // \~{} and \^{} stand for the character itself, e.g., a tilde in an address:
        this.position = afterAccent;
        if (accent is "~" or "^" && this.text.AsSpan(this.position, limit - this.position).StartsWith("{}", StringComparison.Ordinal))
        {
            this.position += 2;
            return Escaped(start, this.position, accent);
        }

        return null;
    }

    private bool TryReadAccentBase(int limit, bool allowsBareLetter, out char letter)
    {
        letter = '\0';
        var rest = this.text.AsSpan(this.position, limit - this.position);
        if (allowsBareLetter && rest.Length > 0 && char.IsAsciiLetter(rest[0]))
        {
            letter = rest[0];
            this.position++;
            return true;
        }

        // A dotless i or j, as in \"\i:
        if (rest is ['\\', 'i' or 'j', ..] && (rest.Length == 2 || !char.IsAsciiLetter(rest[2])))
        {
            letter = rest[1];
            this.position += 2;
            this.SkipSpaceAfterControlWord(limit);
            return true;
        }

        if (rest is ['{', var single, '}', ..] && char.IsAsciiLetter(single))
        {
            letter = single;
            this.position += 3;
            return true;
        }

        if (rest is ['{', '\\', 'i' or 'j', '}', ..])
        {
            letter = rest[2];
            this.position += 4;
            return true;
        }

        return false;
    }

    private LatexNode ParseCommand(int start, string name, int limit)
    {
        var command = LatexCommands.Of(name);
        if (this.position < limit && this.text[this.position] == '*')
            this.position++;

        this.SkipOverlay(limit);
        var arguments = this.ParseArguments(command, limit);
        return new(LatexNodeKind.COMMAND, start, this.position) { Name = name, Arguments = arguments };
    }

    private List<LatexArgument> ParseArguments(LatexCommand command, int limit)
    {
        var arguments = new List<LatexArgument>();
        var mandatoryCount = 0;
        while (true)
        {
            var before = this.position;
            if (command.IsKnown)
                this.SkipSpaceWithinParagraph(limit);

            var next = this.position < limit ? this.text[this.position] : '\0';
            var argument = next switch
            {
                '[' when command.TakesOptionalArguments && (mandatoryCount < command.MandatoryArguments || command.MandatoryArguments == 0) => this.ParseOptionalArgument(limit),
                '(' when command.TakesParenthesizedArgument && mandatoryCount == 0 => this.ParseParenthesizedArgument(limit),
                '{' when mandatoryCount < command.MandatoryArguments && command.HasRawFirstArgument && mandatoryCount == 0 => this.ParseRawArgument(limit),
                '{' when mandatoryCount < command.MandatoryArguments => this.ParseMandatoryArgument(limit),

                _ => null,
            };

            if (argument is null)
            {
                this.position = before;
                return arguments;
            }

            if (!argument.IsOptional)
                mandatoryCount++;

            arguments.Add(argument);
        }
    }

    private LatexArgument? ParseMandatoryArgument(int limit)
    {
        var start = this.position;
        this.position++;
        var children = this.ParseSequence(limit, '}', null);
        if (this.position >= limit)
            return null;

        this.position++;
        return new(false, start + 1, this.position - 1, children);
    }

    /// <summary>
    /// Reads an argument in brackets, which ends within its paragraph.
    /// </summary>
    private LatexArgument? ParseOptionalArgument(int limit)
    {
        var argumentLimit = this.ParagraphEnd(limit);
        var start = this.position;
        this.position++;
        var children = this.ParseSequence(argumentLimit, ']', null);
        if (this.position >= argumentLimit)
            return null;

        this.position++;
        return new(true, start + 1, this.position - 1, children);
    }

    private LatexArgument? ParseParenthesizedArgument(int limit)
    {
        var start = this.position;
        var end = this.text.AsSpan(start, limit - start).IndexOfAny(')', '\r', '\n');
        if (end < 0 || this.text[start + end] != ')')
            return null;

        this.position = start + end + 1;
        return new(true, start + 1, start + end, []);
    }

    /// <summary>
    /// Reads an argument in braces which LaTeX takes as it stands, e.g., an address with a percent sign.
    /// </summary>
    private LatexArgument? ParseRawArgument(int limit)
    {
        var start = this.position;
        var depth = 0;
        for (var index = start; index < limit; index++)
        {
            switch (this.text[index])
            {
                case '\\':
                    index++;
                    break;

                case '{':
                    depth++;
                    break;

                case '}':
                    depth--;
                    if (depth > 0)
                        break;

                    this.position = index + 1;
                    return new(false, start + 1, index, []);
            }
        }

        return null;
    }

    private LatexNode ParseEnvironment(int start, int limit)
    {
        this.SkipSpaces(limit);
        if (!this.TryReadName(limit, out var name))
            return new(LatexNodeKind.INVALID, start, this.position);

        var role = LatexCommands.EnvironmentOf(name);
        if (role is LatexEnvironmentRole.VERBATIM or LatexEnvironmentRole.MATH)
        {
            // The content is no markup to read, and only this exact end ends it:
            var endMarkup = @"\end{" + name + "}";
            var endIndex = this.text.IndexOf(endMarkup, this.position, limit - this.position, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                this.position = limit;
                return new(LatexNodeKind.INVALID, start, limit);
            }

            this.position = endIndex + endMarkup.Length;
            return new(LatexNodeKind.ENVIRONMENT, start, this.position) { Name = name };
        }

        this.SkipOverlay(limit);
        var arguments = this.ParseEnvironmentArguments(limit);
        var children = this.ParseSequence(limit, null, name);
        if (this.TryReadEnd(this.position, limit, out _, out var end))
            this.position = end;

        return new(LatexNodeKind.ENVIRONMENT, start, this.position) { Name = name, Arguments = arguments, Children = children };
    }

    /// <summary>
    /// Reads the arguments which follow the start of an environment on its line, e.g., the columns of a table.
    /// </summary>
    private List<LatexArgument> ParseEnvironmentArguments(int limit)
    {
        var arguments = new List<LatexArgument>();
        while (true)
        {
            var before = this.position;
            this.SkipSpaces(limit);
            var argument = this.position < limit ? this.text[this.position] switch
            {
                '[' => this.ParseOptionalArgument(limit),
                '{' => this.ParseMandatoryArgument(limit),

                _ => null,
            } : null;

            if (argument is null)
            {
                this.position = before;
                return arguments;
            }

            arguments.Add(argument);
        }
    }

    /// <summary>
    /// Reads the name of an environment in braces.
    /// </summary>
    private bool TryReadName(int limit, out string name)
    {
        name = string.Empty;
        if (this.position >= limit || this.text[this.position] != '{')
            return false;

        var length = Math.Min(limit - this.position - 1, MAX_ENVIRONMENT_NAME_LENGTH + 1);
        var close = this.text.AsSpan(this.position + 1, Math.Max(length, 0)).IndexOfAny('}', '\r', '\n');
        if (close <= 0 || this.text[this.position + 1 + close] != '}')
            return false;

        name = this.text.Substring(this.position + 1, close);
        this.position += close + 2;
        return true;
    }

    /// <summary>
    /// Matches \end{name} at a position.
    /// </summary>
    private bool TryReadEnd(int at, int limit, out string name, out int end)
    {
        name = string.Empty;
        end = at;
        const string END = @"\end";
        if (!this.text.AsSpan(at, limit - at).StartsWith(END, StringComparison.Ordinal))
            return false;

        var index = at + END.Length;
        if (index < limit && char.IsAsciiLetter(this.text[index]))
            return false;

        while (index < limit && this.text[index] is ' ' or '\t')
            index++;

        var before = this.position;
        this.position = index;
        var isEnd = this.TryReadName(limit, out name);
        end = this.position;
        this.position = before;
        return isEnd;
    }

    /// <summary>
    /// Reads \verb with its delimiters, e.g., \verb|x|.
    /// </summary>
    private LatexNode ParseVerb(int start, int limit)
    {
        if (this.position < limit && this.text[this.position] == '*')
            this.position++;

        return this.ParseDelimitedCode(start, limit);
    }

    /// <summary>
    /// Reads \lstinline or \mintinline with its options and its code, in braces or between delimiters.
    /// </summary>
    private LatexNode ParseInlineCode(int start, string name, int limit)
    {
        if (this.position < limit && this.text[this.position] == '[')
        {
            var close = this.text.AsSpan(this.position, limit - this.position).IndexOfAny(']', '\r', '\n');
            if (close < 0 || this.text[this.position + close] != ']')
                return new(LatexNodeKind.INVALID, start, this.position);

            this.position += close + 1;
        }

        // The language of \mintinline:
        if (name == "mintinline" && (this.position >= limit || this.text[this.position] != '{' || this.ParseRawArgument(limit) is null))
            return new(LatexNodeKind.INVALID, start, this.position);

        if (this.position < limit && this.text[this.position] == '{')
        {
            return this.ParseRawArgument(limit) is null
                ? new(LatexNodeKind.INVALID, start, this.position)
                : new(LatexNodeKind.VERBATIM, start, this.position);
        }

        return this.ParseDelimitedCode(start, limit);
    }

    private LatexNode ParseDelimitedCode(int start, int limit)
    {
        if (this.position >= limit || char.IsWhiteSpace(this.text[this.position]) || char.IsAsciiLetter(this.text[this.position]))
            return new(LatexNodeKind.INVALID, start, this.position);

        var delimiter = this.text[this.position];
        var close = this.text.AsSpan(this.position + 1, limit - this.position - 1).IndexOfAny(delimiter, '\r', '\n');
        if (close < 0 || this.text[this.position + 1 + close] != delimiter)
        {
            this.position++;
            return new(LatexNodeKind.INVALID, start, this.position);
        }

        this.position += close + 2;
        return new(LatexNodeKind.VERBATIM, start, this.position);
    }

    /// <summary>
    /// Reads the definition of a macro, e.g., \def\name#1{...}, which is markup as a whole.
    /// </summary>
    private LatexNode ParseDefinition(int start, string name, int limit)
    {
        var afterName = this.position;
        this.SkipSpaces(limit);
        if (this.position + 1 < limit && this.text[this.position] == '\\')
        {
            this.position++;
            if (char.IsAsciiLetter(this.text[this.position]))
            {
                while (this.position < limit && char.IsAsciiLetter(this.text[this.position]))
                    this.position++;
            }
            else
                this.position++;

            // The parameters, e.g., #1#2, up to the body:
            while (this.position < limit && this.text[this.position] is not ('{' or '}' or '%' or '\r' or '\n'))
                this.position++;

            if (this.position < limit && this.text[this.position] == '{' && this.ParseMandatoryArgument(limit) is not null)
                return new(LatexNodeKind.COMMAND, start, this.position) { Name = name };
        }

        this.position = afterName;
        return new(LatexNodeKind.COMMAND, start, afterName) { Name = name };
    }

    /// <summary>
    /// Skips an overlay specification of Beamer right after a command, e.g., &lt;2-&gt; of \item&lt;2-&gt;.
    /// </summary>
    private void SkipOverlay(int limit)
    {
        if (this.position >= limit || this.text[this.position] != '<')
            return;

        var index = this.position + 1;
        while (index < limit && index - this.position <= MAX_OVERLAY_LENGTH && this.text[index] is not ('>' or '<' or '{' or '}' or '\\') && !char.IsWhiteSpace(this.text[index]))
            index++;

        if (index < limit && index > this.position + 1 && this.text[index] == '>')
            this.position = index + 1;
    }

    /// <summary>
    /// Whether a line end starts a blank line, i.e., ends a paragraph.
    /// </summary>
    private bool IsParagraphBreak(int at, int limit)
    {
        if (at >= limit || this.text[at] is not ('\r' or '\n'))
            return false;

        var index = at + (this.text[at] == '\r' && at + 1 < limit && this.text[at + 1] == '\n' ? 2 : 1);
        while (index < limit && this.text[index] is ' ' or '\t')
            index++;

        return index < limit && this.text[index] is '\r' or '\n';
    }

    /// <summary>
    /// Finds where the paragraph at the position ends.
    /// </summary>
    private int ParagraphEnd(int limit)
    {
        for (var index = this.position; index < limit; index++)
        {
            if (this.text[index] is '\r' or '\n' && this.IsParagraphBreak(index, limit))
                return index;
        }

        return limit;
    }

    /// <summary>
    /// Skips the spaces after a command with letters, which TeX drops, or the empty group which keeps them.
    /// </summary>
    private void SkipSpaceAfterControlWord(int limit)
    {
        if (this.text.AsSpan(this.position, limit - this.position).StartsWith("{}", StringComparison.Ordinal))
        {
            this.position += 2;
            return;
        }

        this.SkipSpaceWithinParagraph(limit);
    }

    /// <summary>
    /// Skips spaces and at most one line end, as long as no blank line follows.
    /// </summary>
    private void SkipSpaceWithinParagraph(int limit)
    {
        this.SkipSpaces(limit);
        if (this.position < limit && this.text[this.position] is '\r' or '\n' && !this.IsParagraphBreak(this.position, limit))
        {
            this.SkipLineEnd(limit);
            this.SkipSpaces(limit);
        }
    }

    private void SkipSpaces(int limit)
    {
        while (this.position < limit && this.text[this.position] is ' ' or '\t')
            this.position++;
    }

    private void SkipWhiteSpace(int limit)
    {
        while (this.position < limit && this.text[this.position] is ' ' or '\t' or '\r' or '\n')
            this.position++;
    }

    private void SkipLineEnd(int limit)
    {
        if (this.position < limit && this.text[this.position] == '\r')
            this.position++;

        if (this.position < limit && this.text[this.position] == '\n')
            this.position++;
    }

    private void SkipToLineEnd(int limit)
    {
        var lineEnd = this.text.AsSpan(this.position, limit - this.position).IndexOfAny('\r', '\n');
        this.position = lineEnd < 0 ? limit : this.position + lineEnd;
    }
}