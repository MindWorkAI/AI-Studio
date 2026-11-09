using Microsoft.CodeAnalysis.CSharp;

using SharedTools;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that the I18N collector reads string literals exactly as the compiler does.
/// </summary>
/// <remarks>
/// The collector files a text under the hash of what it reads, and the app looks it up by the hash of the string the
/// compiler made. Every case therefore compares the reader with Roslyn, the compiler itself.
/// </remarks>
[TestFixture]
public sealed class CSharpStringLiteralTests
{
    private static readonly string[] LITERALS =
    [
        // Regular literals, with every kind of escape sequence:
        "\"Plain text\"",
        "\"\"",
        "\"Quotes \\\" and a backslash \\\\\"",
        "\"Line\\nbreak and tab\\t and return\\r\"",
        "\"All simple escapes: \\' \\0 \\a \\b \\e \\f \\v\"",
        "\"Unicode \\u00FC and \\U0001F600\"",
        "\"Hex \\x41, \\x041, \\x0041, and \\xA followed by text\"",
        "\"Commas, \\\"quoted\\\", and parentheses (like this)\"",

        // Verbatim literals:
        "@\"C:\\Users\\Name\"",
        "@\"Doubled \"\"quotes\"\" inside\"",
        "@\"Over\nseveral\r\nlines\"",

        // Single-line raw literals:
        "\"\"\"Text with \"quotes\" inside\"\"\"",
        "\"\"\"By clicking \"Accept GPL and Install,\" you agree\"\"\"",
        "\"\"\"\"Text with \"\"\" inside\"\"\"\"",
        "\"\"\"{} and \\n stay as they are\"\"\"",

        // Multi-line raw literals:
        "\"\"\"\n    ## Welcome!\n\n    The first paragraph.\n    \"\"\"",
        "\"\"\"\r\n    CRLF line endings\r\n    stay as they are\r\n    \"\"\"",
        "\"\"\"\n        Indented\n            more indented\n        back\n        \"\"\"",
        "\"\"\"\n    Before an empty line\n\n    after it\n    \"\"\"",
        "\"\"\"\n    Before a line of spaces\n  \n    after it\n    \"\"\"",
        "\"\"\"\n    Before a line of more spaces\n        \n    after it\n    \"\"\"",
        "\"\"\"   \n    Spaces after the opening quotes\n    \"\"\"",
        "\"\"\"\n\tTabs as indentation\n\t\"\"\"",
        "\"\"\"\n    Quotes \"\" inside, and \"quoted\" text\n    \"\"\"",
        "\"\"\"\"\n    Three quotes \"\"\" inside\n    \"\"\"\"",
        "\"\"\"\nNo indentation at all\n\"\"\"",
    ];

    private static readonly string[] NO_LITERALS =
    [
        "Text without quotes",
        "$\"Interpolated {value}\"",
        "\"Unterminated",
        "\"Line break\n inside a regular literal\"",
        "\"Unknown escape \\q\"",
        "\"Short unicode \\u12\"",
        "@\"Unterminated verbatim",
        "\"\"\"Unterminated raw",
        "\"\"\"\n    Content after the closing line is missing",
        "\"\"\"\n      Less indented than\n    the closing line is not allowed\n      \"\"\"",
    ];

    [TestCaseSource(nameof(LITERALS))]
    public void TheReaderGetsWhatTheCompilerGets(string literal)
    {
        var token = SyntaxFactory.ParseToken(literal);
        Assume.That(token.ContainsDiagnostics, Is.False, "The case itself is no valid literal.");

        var isRead = CSharpStringLiteral.TryRead($"{literal}) and more code", out var value, out var length);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRead, Is.True);
            Assert.That(value, Is.EqualTo(token.ValueText));
            Assert.That(length, Is.EqualTo(token.Span.Length));
        }
    }

    [TestCaseSource(nameof(NO_LITERALS))]
    public void SomethingTheCompilerRefusesOrComputesAtRuntimeIsNotRead(string source)
    {
        Assert.That(CSharpStringLiteral.TryRead(source, out _, out _), Is.False);
    }

    [Test]
    public void TheCasesWhichAreNoLiteralsAreRefusedByTheCompilerAsWell()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var source in NO_LITERALS.Where(source => source.StartsWith('"') || source.StartsWith("@\"", StringComparison.Ordinal)))
            {
                var token = SyntaxFactory.ParseToken(source);
                Assert.That(token.ContainsDiagnostics || token.Span.Length < source.Length, Is.True, $"The compiler accepts: {source}");
            }
        }
    }

    [Test]
    public void TheLiteralEndsWhereTheCompilerEndsIt()
    {
        const string SOURCE = "\"First\", \"Second\")";

        var isRead = CSharpStringLiteral.TryRead(SOURCE, out var value, out var length);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRead, Is.True);
            Assert.That(value, Is.EqualTo("First"));
            Assert.That(SOURCE[length..], Is.EqualTo(", \"Second\")"));
        }
    }

    [Test]
    public void EveryLiteralIsAValidStringToken()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var literal in LITERALS)
                Assert.That(SyntaxFactory.ParseToken(literal).Kind(), Is.AnyOf(SyntaxKind.StringLiteralToken, SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken), literal);
        }
    }
}