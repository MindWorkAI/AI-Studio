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
        var memberDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(member => member is MethodDeclarationSyntax or PropertyDeclarationSyntax);
        
        if(memberDeclaration == null)
            return;
        
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use inline expression body for switch expression",
                createChangedDocument: c => UseInlineExpressionBodyAsync(context.Document, memberDeclaration, c),
                equivalenceKey: nameof(SwitchExpressionMethodCodeFixProvider)),
            diagnostic);
    }
    
    private static async Task<Document> UseInlineExpressionBodyAsync(Document document, MemberDeclarationSyntax memberDecl, CancellationToken cancellationToken)
    {
        var switchExpr = SwitchExpressionOf(memberDecl);
        var headerEnd = HeaderEndOf(memberDecl);
        if (switchExpr is null || headerEnd is null)
            return document;
        
        var sourceText = await document.GetTextAsync(cancellationToken);
        var memberStartLine = sourceText.Lines.GetLineFromPosition(memberDecl.SpanStart);
        
        //
        // Everything in front of the body stays as it was written: the attributes, the modifiers, the type,
        // the name, and of a method its type parameters, parameters, and constraints. Building it anew from
        // some of these parts loses the others.
        //
        var headerText = sourceText.ToString(TextSpan.FromBounds(memberDecl.SpanStart, headerEnd.Value)).TrimEnd();
        
        // Extract the governing expression and the switch body:
        var govExprText = sourceText.ToString(switchExpr.GoverningExpression.Span);
        var switchBodyText = sourceText.ToString(TextSpan.FromBounds(switchExpr.OpenBraceToken.SpanStart, switchExpr.CloseBraceToken.Span.End));
        
        // Determine the indentation of the member:
        var memberIndentation = "";
        for (var i = memberStartLine.Start; i < memberDecl.SpanStart; i++)
        {
            if (char.IsWhiteSpace(sourceText[i]))
                memberIndentation += sourceText[i];
            else
                break;
        }
        
        // Create the new member with an expression body and the switch keyword on the same line:
        var newMemberText = new StringBuilder();
        newMemberText.Append($"{headerText} => {govExprText} switch");
        
        // The switch cases are indented by four spaces more than the member:
        var caseIndentation = memberIndentation + "    ";
        newMemberText.Append(FormatSwitchBody(switchBodyText, memberIndentation, caseIndentation));
        
        // Replace the old declaration with the new text:
        var newText = sourceText.Replace(memberDecl.Span, newMemberText.ToString());
        return document.WithText(newText);
    }
    
    private static SwitchExpressionSyntax? SwitchExpressionOf(MemberDeclarationSyntax memberDecl) => memberDecl switch
    {
        MethodDeclarationSyntax methodDecl => SwitchExpressionOf(methodDecl.Body, methodDecl.ExpressionBody),
        PropertyDeclarationSyntax { ExpressionBody: not null } propertyDecl => SwitchExpressionOf(null, propertyDecl.ExpressionBody),
        PropertyDeclarationSyntax { AccessorList.Accessors.Count: 1 } propertyDecl => SwitchExpressionOf(propertyDecl.AccessorList.Accessors[0].Body, propertyDecl.AccessorList.Accessors[0].ExpressionBody),
        
        _ => null,
    };
    
    private static SwitchExpressionSyntax? SwitchExpressionOf(BlockSyntax? body, ArrowExpressionClauseSyntax? expressionBody)
    {
        if (expressionBody?.Expression is SwitchExpressionSyntax inlineSwitchExpr)
            return inlineSwitchExpr;
        
        // A block body with a return statement that contains a switch expression:
        if (body is { Statements.Count: 1 } && body.Statements[0] is ReturnStatementSyntax { Expression: SwitchExpressionSyntax returnedSwitchExpr })
            return returnedSwitchExpr;
        
        return null;
    }
    
    private static int? HeaderEndOf(MemberDeclarationSyntax memberDecl) => memberDecl switch
    {
        MethodDeclarationSyntax { ConstraintClauses.Count: > 0 } methodDecl => methodDecl.ConstraintClauses.Last().Span.End,
        MethodDeclarationSyntax methodDecl => methodDecl.ParameterList.Span.End,
        PropertyDeclarationSyntax propertyDecl => propertyDecl.Identifier.Span.End,
        
        _ => null,
    };
    
    private static string FormatSwitchBody(string switchBody, string memberIndentation, string caseIndentation)
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
        result.Append($"{memberIndentation}{{");
        
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
        result.Append($"{memberIndentation}}};");
        
        return result.ToString();
    }
}