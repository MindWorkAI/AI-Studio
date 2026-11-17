using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using SourceCodeRules.UsageAnalyzers;

namespace SourceCodeRules.UsageCodeFixes;

/// <summary>
/// Writes LF instead of the line break of the system: AppendLineLF instead of StringBuilder.AppendLine, and "\n"
/// instead of Environment.NewLine.
/// </summary>
/// <remarks>
/// The app imports SharedTools.Text everywhere. Elsewhere, such as in SharedTools or in the tests, the fix adds the
/// using directive itself when AppendLineLF would not be found without it.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SystemLineBreakCodeFixProvider)), Shared]
public sealed class SystemLineBreakCodeFixProvider : CodeFixProvider
{
    private const string TITLE_APPEND_LINE = "Use AppendLineLF";

    private const string TITLE_NEW_LINE = "Use a string with LF";

    private const string NAMESPACE_OF_APPEND_LINE_LF = "SharedTools.Text";

    public override ImmutableArray<string> FixableDiagnosticIds => [Identifier.SYSTEM_LINE_BREAK_ANALYZER];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        if (!diagnostic.Properties.TryGetValue(SystemLineBreakAnalyzer.KIND_KEY, out var kind))
            return;

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (kind is SystemLineBreakAnalyzer.KIND_APPEND_LINE && NameOfCall(node.FirstAncestorOrSelf<InvocationExpressionSyntax>()) is { } name)
            context.RegisterCodeFix(CodeAction.Create(TITLE_APPEND_LINE, token => UseAppendLineLFAsync(context.Document, name, token), TITLE_APPEND_LINE), diagnostic);
        else if (kind is SystemLineBreakAnalyzer.KIND_NEW_LINE && node is ExpressionSyntax newLine)
            context.RegisterCodeFix(CodeAction.Create(TITLE_NEW_LINE, token => UseLineFeedAsync(context.Document, newLine, token), TITLE_NEW_LINE), diagnostic);
    }

    /// <summary>
    /// Gets the name of the method a call invokes, also behind a ?. of a conditional access.
    /// </summary>
    /// <param name="call">The call, or null.</param>
    /// <returns>The name, or null when the call names no member.</returns>
    private static SimpleNameSyntax? NameOfCall(InvocationExpressionSyntax? call) => call?.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name,
        MemberBindingExpressionSyntax binding => binding.Name,
        _ => null
    };

    private static async Task<Document> UseAppendLineLFAsync(Document document, SimpleNameSyntax name, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        if (root is null)
            return document;

        var annotation = new SyntaxAnnotation();
        var renamed = document.WithSyntaxRoot(root.ReplaceNode(name, SyntaxFactory.IdentifierName("AppendLineLF").WithTriviaFrom(name).WithAdditionalAnnotations(annotation)));
        var renamedRoot = await renamed.GetSyntaxRootAsync(token).ConfigureAwait(false);
        var model = await renamed.GetSemanticModelAsync(token).ConfigureAwait(false);
        if (renamedRoot is not CompilationUnitSyntax unit || model is null)
            return renamed;

        //
        // Without the namespace of AppendLineLF in scope, the call binds to nothing, and only then is the using
        // directive missing. It ends with the line break the file already uses:
        //
        var call = unit.GetAnnotatedNodes(annotation).First().FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (call is null || model.GetSymbolInfo(call, token).Symbol is not null)
            return renamed;

        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(NAMESPACE_OF_APPEND_LINE_LF).WithLeadingTrivia(SyntaxFactory.Space)).WithTrailingTrivia(SyntaxFactory.EndOfLine(LineBreakOf(unit)));
        return renamed.WithSyntaxRoot(unit.AddUsings(directive));
    }

    private static async Task<Document> UseLineFeedAsync(Document document, ExpressionSyntax newLine, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        if (root is null)
            return document;

        var lineFeed = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("\n")).WithTriviaFrom(newLine);
        return document.WithSyntaxRoot(root.ReplaceNode(newLine, lineFeed));
    }

    /// <summary>
    /// Gets the line break a file uses, so that an added line does not bring a second kind into it.
    /// </summary>
    /// <param name="root">The root of the file.</param>
    /// <returns>The first line break of the file, or LF when it has none.</returns>
    private static string LineBreakOf(SyntaxNode root)
    {
        var lineBreak = root.DescendantTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
        return lineBreak.IsKind(SyntaxKind.EndOfLineTrivia) ? lineBreak.ToString() : "\n";
    }
}