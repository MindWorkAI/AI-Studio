using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SourceCodeRules.UsageAnalyzers;

/// <summary>
/// Reports a string which names a member in braces, such as {this.location}, without being interpolated.
/// </summary>
/// <remarks>
/// A string without a $ in front keeps its braces as text, and so does a single pair of braces in a raw
/// string written with $$. The compiler accepts both. At runtime, nothing looks wrong either: the model, the
/// user, or the file simply receives the text {this.location} instead of the location. Because every member
/// is accessed through this. (see MWAIS0005), looking for {this. finds each member which was meant to be
/// interpolated, and leaves placeholders like {0} for string.Format alone.
/// </remarks>
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1038
public sealed class MissingInterpolationAnalyzer : DiagnosticAnalyzer
{
    private const string DIAGNOSTIC_ID = Identifier.MISSING_INTERPOLATION_ANALYZER;
    private const string CATEGORY = "Usage";
    private const string MEMBER_IN_BRACES = "{this.";

    private const string TITLE = "A member in braces needs an interpolated string";

    private const string MESSAGE_FORMAT = "The text \"{0}\" is not interpolated and stays as it is: put a $ in front of the string, or one more $ in front of a raw string";

    private const string DESCRIPTION = "A string which names a member in braces, such as {this.location}, without being interpolated passes the braces on as text instead of the value of the member.";

    private static readonly DiagnosticDescriptor RULE = new(DIAGNOSTIC_ID, TITLE, MESSAGE_FORMAT, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true, description: DESCRIPTION);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [RULE];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInterpolatedText, SyntaxKind.InterpolatedStringText);
    }

    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var stringLiteral = (LiteralExpressionSyntax)context.Node;
        Check(context, stringLiteral.Token.ValueText, stringLiteral.GetLocation());
    }

    /// <summary>
    /// Checks the text between the holes of an interpolated string.
    /// </summary>
    /// <remarks>
    /// There, braces end up as text when they are escaped, like {{this.location}} in $"...", or when they
    /// are fewer than the dollar signs, like {this.location} in $$"""...""".
    /// </remarks>
    private static void AnalyzeInterpolatedText(SyntaxNodeAnalysisContext context)
    {
        var interpolatedText = (InterpolatedStringTextSyntax)context.Node;
        Check(context, interpolatedText.TextToken.ValueText, interpolatedText.GetLocation());
    }

    private static void Check(SyntaxNodeAnalysisContext context, string text, Location location)
    {
        var start = FindMemberInBraces(text);
        if (start < 0)
            return;

        var end = text.IndexOf('}', start);
        var memberInBraces = end < 0 ? text.Substring(start) : text.Substring(start, end - start + 1);
        context.ReportDiagnostic(Diagnostic.Create(RULE, location, memberInBraces));
    }

    /// <summary>
    /// Finds {this. followed by the first character of a member name.
    /// </summary>
    /// <returns>The position of the opening brace, or -1 when the text names no member in braces.</returns>
    private static int FindMemberInBraces(string text)
    {
        var position = text.IndexOf(MEMBER_IN_BRACES, StringComparison.Ordinal);
        while (position >= 0)
        {
            var next = position + MEMBER_IN_BRACES.Length;
            if (next < text.Length && (char.IsLetter(text[next]) || text[next] == '_'))
                return position;

            position = text.IndexOf(MEMBER_IN_BRACES, next, StringComparison.Ordinal);
        }

        return -1;
    }
}