using System.Globalization;
using System.Text;

using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>Reads one exported configuration assignment as data. No Lua state is created.</summary>
/// <remarks>
/// A snippet is text somebody else wrote, pasted by a user who cannot tell what it does. Running it,
/// even in the sandbox of the plugins, would let it do whatever Lua allows: loop forever, fill the
/// memory, or compute its values from anything within reach. Configuration plugins may run code
/// because an organization deploys them; a snippet earns no such trust. This parser therefore
/// accepts only what an export writes: one assignment of a table made of strings, numbers,
/// booleans, and tables, with comments in between. Whatever it accepts, it reads as Lua would, so a
/// snippet means the same here as in a configuration plugin. Anything else is refused, never
/// guessed: a construct which Lua would read differently must not slip through as data.
/// </remarks>
public sealed class ConfigurationSnippetParser
{
    private readonly string source;
    private int position;

    private ConfigurationSnippetParser(string source) => this.source = source;

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationSnippetParser).Namespace, nameof(ConfigurationSnippetParser));

    /// <summary>
    /// Reads a snippet of the form <c>CONFIG["SECTION"][#CONFIG["SECTION"]+1] = { ... }</c>.
    /// </summary>
    /// <param name="source">The pasted snippet.</param>
    /// <param name="type">The type of configuration object the snippet must describe.</param>
    /// <param name="table">The table the snippet assigns, when it could be read.</param>
    /// <param name="issue">Why the snippet could not be read, otherwise empty.</param>
    /// <returns>True when the snippet could be read.</returns>
    public static bool TryParse(string source, PluginConfigurationObjectType type, out LuaTable table, out string issue)
    {
        table = new LuaTable();
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(source) || source.Length > 1_000_000)
        {
            issue = TB("Paste one exported configuration snippet (up to 1 MB).");
            return false;
        }

        try
        {
            var expectedSection = PluginConfigurationObjectDescriptors.Get(type).LuaTableName;
            var parser = new ConfigurationSnippetParser(source);
            parser.ExpectWord("CONFIG");
            var section = parser.ReadBracketedString();
            if (section != expectedSection)
                throw new FormatException(PluginConfigurationObjectDescriptors.TryGet(section, out var otherDescriptor)
                    ? string.Format(TB("The pasted configuration describes the type '{0}', but only the type '{1}' can be imported here."), otherDescriptor.Type.GetName(), type.GetName())
                    : string.Format(TB("The pasted configuration does not describe the type '{0}', which can be imported here."), type.GetName()));

            parser.Expect('[');
            parser.Expect('#');
            parser.ExpectWord("CONFIG");
            if (parser.ReadBracketedString() != expectedSection)
                throw new FormatException(TB("The configuration section names do not match."));
            parser.Expect('+');
            parser.Expect('1');
            parser.Expect(']');
            parser.Expect('=');
            table = parser.ReadTable(0);
            parser.SkipTrivia();
            if (parser.position != source.Length)
                throw new FormatException(TB("The snippet must contain exactly one table assignment and no executable code."));
            return true;
        }
        catch (FormatException exception)
        {
            issue = exception.Message;
            return false;
        }
    }

    private LuaTable ReadTable(int depth)
    {
        if (depth > 32)
            throw new FormatException(TB("The snippet contains too many nested tables."));
        this.Expect('{');
        var table = new LuaTable();
        var arrayIndex = 1;
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            this.SkipTrivia();
            if (this.Take('}'))
                return table;

            // "[[" or "[==[" opens a long string, which is an array value rather than a bracketed key:
            if (this.Peek() == '[' && !this.IsLongStringStart())
            {
                this.position++;
                var key = this.ReadString();
                this.Expect(']');
                this.Expect('=');
                if (!names.Add(key))
                    throw new FormatException(string.Format(TB("The field '{0}' occurs more than once."), key));
                table[key] = this.ReadValue(depth + 1);
            }
            else
                table[arrayIndex++] = this.ReadValue(depth + 1);

            this.SkipTrivia();
            if (this.Take('}'))
                return table;
            if (!this.Take(',') && !this.Take(';'))
                throw new FormatException(string.Format(TB("Expected a comma or closing brace at character {0}."), this.position + 1));
        }
    }

    private LuaValue ReadValue(int depth)
    {
        this.SkipTrivia();
        if (this.Peek() == '{')
            return this.ReadTable(depth);
        if (this.Peek() is '"' or '\'' || this.Peek() == '[')
            return this.ReadString();
        if (this.TakeWord("true"))
            return true;
        if (this.TakeWord("false"))
            return false;

        // In Lua, nil leaves a field out, and in a list it ends what the parsers read of it:
        if (this.TakeWord("nil"))
            throw new FormatException(string.Format(TB("The value nil at character {0} is not supported. Leave the field out instead."), this.position - 2));

        var start = this.position;
        if (this.Peek() == '-')
            this.position++;
        while (char.IsAsciiDigit(this.Peek()))
            this.position++;
        if (this.Peek() == '.')
        {
            this.position++;
            while (char.IsAsciiDigit(this.Peek()))
                this.position++;
        }
        if (this.position > start && double.TryParse(this.source[start..this.position], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            return number;
        throw new FormatException(string.Format(TB("Only literal values are allowed at character {0}; executable Lua is not accepted."), start + 1));
    }

    private bool IsLongStringStart() => this.IsLongBracketAt(this.position);

    /// <summary>
    /// Whether a long bracket opens at this index: '[', any number of '=', and '[' again.
    /// </summary>
    private bool IsLongBracketAt(int index)
    {
        if (index >= this.source.Length || this.source[index] != '[')
            return false;

        index++;
        while (index < this.source.Length && this.source[index] == '=')
            index++;

        return index < this.source.Length && this.source[index] == '[';
    }

    /// <summary>
    /// Skips one line break, which Lua counts as one whether it is \n, \r, \r\n, or \n\r.
    /// </summary>
    private void SkipLineBreak()
    {
        var first = this.Peek();
        if (first is not ('\r' or '\n'))
            return;

        this.position++;
        var second = this.Peek();
        if (second is '\r' or '\n' && second != first)
            this.position++;
    }

    /// <summary>
    /// Turns every line break of a long string into \n, as Lua does.
    /// </summary>
    private static string NormalizeLineBreaks(string text)
    {
        if (!text.Contains('\r'))
            return text;

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is not ('\r' or '\n'))
            {
                builder.Append(c);
                continue;
            }

            builder.Append('\n');
            if (i + 1 < text.Length && text[i + 1] is '\r' or '\n' && text[i + 1] != c)
                i++;
        }

        return builder.ToString();
    }

    private string ReadBracketedString()
    {
        this.Expect('[');
        var result = this.ReadString();
        this.Expect(']');
        return result;
    }

    private string ReadString()
    {
        this.SkipTrivia();
        var quote = this.Peek();
        if (quote == '[')
        {
            this.position++;
            var equalsStart = this.position;
            while (this.Peek() == '=')
                this.position++;
            var equals = this.source[equalsStart..this.position];
            if (this.Peek() != '[')
                throw new FormatException(string.Format(TB("Expected '{0}' at character {1}."), '[', this.position + 1));
            this.position++;

            // Like Lua, a line break right after the opening bracket is not part of the string:
            this.SkipLineBreak();
            var end = this.source.IndexOf("]" + equals + "]", this.position, StringComparison.Ordinal);
            if (end < 0)
                throw new FormatException(TB("Unterminated long string."));
            var value = NormalizeLineBreaks(this.source[this.position..end]);
            this.position = end + equals.Length + 2;
            return value;
        }
        if (quote is not ('"' or '\''))
            throw new FormatException(string.Format(TB("Expected a quoted string at character {0}."), this.position + 1));
        this.position++;
        var builder = new StringBuilder();
        while (this.position < this.source.Length)
        {
            var c = this.source[this.position++];
            if (c == quote)
                return builder.ToString();
            if (c is '\r' or '\n')
                throw new FormatException(TB("A quoted string contains an unescaped newline."));
            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }
            if (this.position == this.source.Length)
                break;
            c = this.source[this.position++];
            builder.Append(c switch
            {
                'n' => '\n', 'r' => '\r', 't' => '\t', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v',
                '\\' => '\\', '"' => '"', '\'' => '\'',
                _ => throw new FormatException(string.Format(TB("Unsupported string escape sequence: {0}"), "\\" + c)),
            });
        }
        throw new FormatException(TB("Unterminated quoted string."));
    }

    private void SkipTrivia()
    {
        while (this.position < this.source.Length)
        {
            if (char.IsWhiteSpace(this.source[this.position]))
            {
                this.position++;
                continue;
            }
            if (this.source.AsSpan(this.position).StartsWith("--"))
            {
                //
                // A block comment may end in the middle of a line and continue with data, which a
                // line comment would swallow. Rather than reading it differently than Lua does, it
                // is refused:
                //
                if (this.IsLongBracketAt(this.position + 2))
                    throw new FormatException(string.Format(TB("The block comment at character {0} is not supported. Please use line comments, which start with -- and end at the end of the line."), this.position + 1));

                this.position += 2;
                while (this.position < this.source.Length && this.source[this.position] != '\n')
                    this.position++;
                continue;
            }
            break;
        }
    }

    private char Peek() => this.position < this.source.Length ? this.source[this.position] : '\0';

    private bool Take(char c)
    {
        this.SkipTrivia();
        if (this.Peek() != c)
            return false;
        this.position++;
        return true;
    }

    private void Expect(char c)
    {
        if (!this.Take(c))
            throw new FormatException(string.Format(TB("Expected '{0}' at character {1}."), c, this.position + 1));
    }

    private bool TakeWord(string word)
    {
        this.SkipTrivia();
        if (!this.source.AsSpan(this.position).StartsWith(word) ||
            (this.position + word.Length < this.source.Length && (char.IsLetterOrDigit(this.source[this.position + word.Length]) || this.source[this.position + word.Length] == '_')))
            return false;
        this.position += word.Length;
        return true;
    }

    private void ExpectWord(string word)
    {
        if (!this.TakeWord(word))
            throw new FormatException(string.Format(TB("Expected '{0}' at character {1}."), word, this.position + 1));
    }
}
