using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceCodeRules.StyleCodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SwitchExpressionMethodCodeFixProvider)), Shared]
public class SwitchExpressionMethodCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [Identifier.SWITCH_EXPRESSION_METHOD_ANALYZER];
    
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;
        
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var methodDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();
        
        if(methodDeclaration == null)
            return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use inline expression body for switch expression",
                createChangedDocument: c => UseInlineExpressionBodyAsync(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(SwitchExpressionMethodCodeFixProvider)),
            diagnostic);
    }
    
    private static async Task<Document> UseInlineExpressionBodyAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var sourceText = await document.GetTextAsync(cancellationToken);
        var parameterText = methodDecl.ParameterList.ToString();
        var methodStartLine = sourceText.Lines.GetLineFromPosition(methodDecl.SpanStart);
        
        SwitchExpressionSyntax? switchExpr = null;
        ExpressionSyntax? governingExpression = null;
        var switchBodyText = string.Empty;
        
        if (methodDecl.Body != null)
        {
            // Case: Block-Body with a Return-Statement that contains a Switch-Expression
            var returnStmt = (ReturnStatementSyntax)methodDecl.Body.Statements[0];
            if (returnStmt.Expression is not SwitchExpressionSyntax matchingSwitchExpr)
                return document;
            
            switchExpr = matchingSwitchExpr;
            governingExpression = switchExpr.GoverningExpression;
            
            // Extract the switch body text:
            var switchStart = switchExpr.SwitchKeyword.SpanStart;
            var switchEnd = switchExpr.CloseBraceToken.Span.End;
            switchBodyText = sourceText.ToString(TextSpan.FromBounds(switchStart, switchEnd));
        }
        else if (methodDecl.ExpressionBody != null)
        {
            // Case 2: Expression-Body with a poorly formatted Switch-Expression
            switchExpr = (SwitchExpressionSyntax)methodDecl.ExpressionBody.Expression;
            governingExpression = switchExpr.GoverningExpression;
            
            // Extract the switch body text:
            var switchStart = switchExpr.SwitchKeyword.SpanStart;
            var switchEnd = switchExpr.CloseBraceToken.Span.End;
            switchBodyText = sourceText.ToString(TextSpan.FromBounds(switchStart, switchEnd));
        }
        
        if (switchExpr is null || governingExpression is null)
            return document;
        
        // Extract the governing expression and the switch body:
        var govExprText = sourceText.ToString(governingExpression.Span);
        
        // Create the new method with inline expression body and correct formatting:
        var returnTypeText = methodDecl.ReturnType.ToString();
        var modifiersText = string.Join(" ", methodDecl.Modifiers);
        var methodNameText = methodDecl.Identifier.Text;
        
        // Determine the indentation of the method:
        var methodIndentation = "";
        for (var i = methodStartLine.Start; i < methodDecl.SpanStart; i++)
        {
            if (char.IsWhiteSpace(sourceText[i]))
                methodIndentation += sourceText[i];
            else
                break;
        }
        
        // Erstelle die neue Methode mit Expression-Body und korrekter Formatierung
        var newMethodText = new StringBuilder();
        newMethodText.Append($"{modifiersText} {returnTypeText} {methodNameText}{parameterText} => {govExprText} switch");
        
        // Formatiere die geschweiften Klammern und den Switch-Body
        var switchBody = switchBodyText.Substring("switch".Length).Trim();
        
        // Bestimme die Einrückung für die Switch-Cases (4 Spaces oder 1 Tab mehr als die Methode)
        var caseIndentation = methodIndentation + "    "; // 4 Spaces Einrückung
        
        // Verarbeite die Klammern und formatiere den Body
        var formattedSwitchBody = FormatSwitchBody(switchBody, methodIndentation, caseIndentation);
        newMethodText.Append(formattedSwitchBody);
        
        // Ersetze die alte Methoden-Deklaration mit dem neuen Text
        var newText = sourceText.Replace(methodDecl.Span, newMethodText.ToString());
        return document.WithText(newText);
    }
    
    private static string FormatSwitchBody(string switchBody, string methodIndentation, string caseIndentation)
    {
        var result = new StringBuilder();
        
        // Remove braces from the switch body:
        var bodyWithoutBraces = switchBody.Trim();
        if (bodyWithoutBraces.StartsWith("{"))
            bodyWithoutBraces = bodyWithoutBraces.Substring(1);
        if (bodyWithoutBraces.EndsWith("}"))
            bodyWithoutBraces = bodyWithoutBraces.Substring(0, bodyWithoutBraces.Length - 1);
        
        bodyWithoutBraces = bodyWithoutBraces.Trim();
        
        // Add braces with correct indentation:
        result.AppendLine();
        result.Append($"{methodIndentation}{{");
        
        // Process each line of the switch body:
        var lines = bodyWithoutBraces.Split(["\r\n", "\n"], System.StringSplitOptions.None);
        foreach (var line in lines)
        {
            result.AppendLine();
            
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;
            
            // Add correct indentation for each case:
            result.Append(caseIndentation);
            result.Append(trimmedLine);
        }
        
        // Add the closing brace with correct indentation:
        result.AppendLine();
        result.Append($"{methodIndentation}}};");
        
        return result.ToString();
    }
}