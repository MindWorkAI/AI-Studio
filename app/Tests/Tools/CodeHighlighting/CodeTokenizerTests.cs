using AIStudio.Tools.CodeHighlighting;

namespace AIStudio.Tests.Tools.CodeHighlighting;

/// <summary>
/// Checks how the tokenizer reads code, with a small language made up for the tests. The tests of
/// each real language follow in their own files.
/// </summary>
[TestFixture]
public sealed class CodeTokenizerTests
{
    private static readonly CodeLanguageRules TEST_LANGUAGE = new()
    {
        Keywords = CodeLanguageRules.Words("def", "class", "if", "return"),
        Types = CodeLanguageRules.Words("int"),
        Literals = CodeLanguageRules.Words("true", "null"),
        FunctionDeclarations = CodeLanguageRules.Words("def"),
        TypeDeclarations = CodeLanguageRules.Words("class"),
        LineComments = ["//"],
        BlockComments = [new("/*", "*/")],
        Strings =
        [
            new("\"\"\"", "\"\"\"", IsMultiline: true),
            new("\"", "\""),
            new("'", "'", Escape: null, DoubledCloseIsLiteral: true),
        ],
        StringPrefixes = CodeLanguageRules.Words("r"),
        VariablePrefix = '$',
        AnnotationPrefix = '@',
    };

    /// <summary>
    /// The same language with all habits of the real languages switched on, for the test which feeds
    /// it random characters.
    /// </summary>
    private static readonly CodeLanguageRules TEST_LANGUAGE_WITH_ALL_HABITS = new()
    {
        Keywords = CodeLanguageRules.Words("def", "class", "if", "return"),
        Literals = CodeLanguageRules.Words("true"),
        FunctionDeclarations = CodeLanguageRules.Words("def"),
        TypeDeclarations = CodeLanguageRules.Words("class"),
        LineComments = ["#", "--"],
        LineCommentsNeedSeparator = true,
        BlockComments = [new("/*", "*/"), new("<#", "#>")],
        Strings = [new("\"\"\"", "\"\"\"", IsMultiline: true), new("\"", "\"", Escape: '`'), new("'", "'", Escape: null, DoubledCloseIsLiteral: true)],
        QuotedNames = [new("`", "`", Escape: null)],
        StringPrefixes = CodeLanguageRules.Words("r", "L"),
        HasLongBrackets = true,
        VariablePrefix = '$',
        VariableNameCharacters = ":",
        AnnotationPrefix = '@',
        HasPreprocessor = true,
        WordStartCharacters = "-$",
        WordCharacters = "-",
        HighlightPascalCaseTypes = true,
        HighlightCommandNames = true,
        HyphenOperators = CodeLanguageRules.WordsInAnyCase("eq", "ne"),
        HasBracketTypes = true,
        IsStyleSheet = true,
    };

    [TestCase("def greet(name):", "{keyword:def} {function:greet}(name):")]
    [TestCase("class Point:", "{keyword:class} {type:Point}:")]
    [TestCase("def (x)", "{keyword:def} (x)", Description = "What a keyword expects ends at anything but a name.")]
    [TestCase("if x return true", "{keyword:if} x {keyword:return} {literal:true}")]
    [TestCase("int count = 42", "{type:int} count = {number:42}")]
    [TestCase("print(x)", "{function:print}(x)")]
    [TestCase("print (x)", "print (x)", Description = "Only a parenthesis right after a name makes it a call.")]
    [TestCase("Define x", "Define x", Description = "A keyword matches only as it is written.")]
    [TestCase("x1 = y2", "x1 = y2", Description = "Digits within a name make no number.")]
    public void WordsGetTheColorOfWhatTheyAre(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TEST_LANGUAGE), Is.EqualTo(expected));
    }

    [TestCase("a // note\nb", "a {comment:// note}\nb")]
    [TestCase("a // note\r\nb", "a {comment:// note}\r\nb", Description = "The line break is no part of the comment.")]
    [TestCase("a /* one\ntwo */ b", "a {comment:/* one\ntwo */} b")]
    [TestCase("a /* never closed\nb", "a {comment:/* never closed\nb}", Description = "A model which is still writing has not closed it yet.")]
    [TestCase("\"a // b\" // c", "{string:\"a // b\"} {comment:// c}", Description = "Within a string, nothing starts a comment.")]
    public void CommentsRunToTheirEnd(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TEST_LANGUAGE), Is.EqualTo(expected));
    }

    [TestCase("\"a\\\"b\" c", "{string:\"a\\\"b\"} c", Description = "An escaped quotation mark does not close the string.")]
    [TestCase("'it''s' x", "{string:'it''s'} x", Description = "Two apostrophes stand for one.")]
    [TestCase("'a\\' x", "{string:'a\\'} x", Description = "A string without escapes ends at its first closing mark.")]
    [TestCase("\"open\nnext", "{string:\"open}\nnext", Description = "A string of one line ends with its line, closed or not.")]
    [TestCase("\"\"\"one\ntwo\"\"\" x", "{string:\"\"\"one\ntwo\"\"\"} x")]
    [TestCase("\"\"\"never closed\nx", "{string:\"\"\"never closed\nx}")]
    [TestCase("r\"raw\" r = 1", "{string:r\"raw\"} r = {number:1}", Description = "A prefix belongs to the string only right before it.")]
    [TestCase("\"if\"", "{string:\"if\"}", Description = "A keyword within a string stays a part of it.")]
    public void StringsRunToTheirEnd(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TEST_LANGUAGE), Is.EqualTo(expected));
    }

    [TestCase("42", "{number:42}")]
    [TestCase("0x1F", "{number:0x1F}")]
    [TestCase("0b1010", "{number:0b1010}")]
    [TestCase("1.5e-3", "{number:1.5e-3}")]
    [TestCase("1_000", "{number:1_000}")]
    [TestCase("10UL 1.5f", "{number:10UL} {number:1.5f}")]
    [TestCase(".5", "{number:.5}")]
    [TestCase("1..5", "{number:1}..{number:5}", Description = "A range is no decimal point.")]
    [TestCase("a.b", "a.b")]
    public void NumbersIncludeTheirNotation(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TEST_LANGUAGE), Is.EqualTo(expected));
    }

    [TestCase("$name", "{variable:$name}")]
    [TestCase("${a b}", "{variable:${a b}}")]
    [TestCase("$? $1", "{variable:$?} {variable:$1}")]
    [TestCase("$(cmd)", "$(cmd)", Description = "A command substitution is no variable.")]
    [TestCase("@Override", "{meta:@Override}")]
    [TestCase("@app.route('/')", "{meta:@app.route}({string:'/'})")]
    [TestCase("a @ b", "a @ b", Description = "An operator is no annotation.")]
    public void PrefixesMarkVariablesAndAnnotations(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TEST_LANGUAGE), Is.EqualTo(expected));
    }

    [TestCase("# note", "{comment:# note}")]
    [TestCase("echo $#", "echo {variable:$#}")]
    [TestCase("a#b", "a#b")]
    [TestCase("a; # note", "a; {comment:# note}")]
    public void ACommentWhichNeedsASeparatorStartsOnlyAtTheStartOfAWord(string code, string expected)
    {
        var shellLike = new CodeLanguageRules { LineComments = ["#"], LineCommentsNeedSeparator = true, VariablePrefix = '$' };
        Assert.That(CodeMarkup.Of(code, shellLike), Is.EqualTo(expected));
    }

    [TestCase("#include <a.h>\nint x;", "{meta:#include <a.h>}\n{type:int} x;")]
    [TestCase("  #define X 1", "  {meta:#define X 1}")]
    [TestCase("x # y", "x # y", Description = "Only a # at the start of a line begins a directive.")]
    public void ADirectiveRunsToTheEndOfItsLine(string code, string expected)
    {
        var cLike = new CodeLanguageRules { Types = CodeLanguageRules.Words("int"), HasPreprocessor = true };
        Assert.That(CodeMarkup.Of(code, cLike), Is.EqualTo(expected));
    }

    [TestCase("HashMap map", "{type:HashMap} map")]
    [TestCase("URL x", "URL x", Description = "A constant in capitals is no type.")]
    public void PascalCaseNamesTypesWhereTheLanguageSaysSo(string code, string expected)
    {
        var javaLike = new CodeLanguageRules { HighlightPascalCaseTypes = true };
        Assert.That(CodeMarkup.Of(code, javaLike), Is.EqualTo(expected));
    }

    [Test]
    public void EmptyCodeHasNoParts()
    {
        Assert.That(CodeTokenizer.Tokenize(string.Empty, TEST_LANGUAGE), Is.Empty);
    }

    /// <summary>
    /// A model writes anything, and a streamed answer stops anywhere. Whatever the code, the parts stay
    /// in order, inside the code, and never overlap.
    /// </summary>
    [Test]
    public void AnyCodeGivesPartsInOrder([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
    {
        const string CHARACTERS = "\"'`\\/*#$@{}()[]=-.:;!%<>abcdefLrXYZ019_ \n\r\t";
        var random = new Random(seed);
        foreach (var rules in new[] { TEST_LANGUAGE, TEST_LANGUAGE_WITH_ALL_HABITS })
        {
            for (var sample = 0; sample < 200; sample++)
            {
                var code = new string(Enumerable.Range(0, random.Next(1, 120)).Select(_ => CHARACTERS[random.Next(CHARACTERS.Length)]).ToArray());
                var end = 0;
                foreach (var token in CodeTokenizer.Tokenize(code, rules))
                {
                    Assert.That(token.Start, Is.GreaterThanOrEqualTo(end), $"A part overlaps the one before it in: {code}");
                    Assert.That(token.Length, Is.GreaterThan(0), $"A part is empty in: {code}");
                    end = token.Start + token.Length;
                    Assert.That(end, Is.LessThanOrEqualTo(code.Length), $"A part runs past the end of: {code}");
                }
            }
        }
    }
}