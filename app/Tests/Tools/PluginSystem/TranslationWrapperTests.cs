using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that every TB wrapper of the app looks its texts up under the class it belongs to.
/// </summary>
/// <remarks>
/// The I18N collector files the texts of a TB wrapper under the namespace and the class of its source file, and
/// the language plugins translate them under that key. A wrapper which names another class in its typeof and
/// nameof looks every text up under a key no plugin knows, so its texts stay English in every language, and
/// nothing reports it. The wrapper of the Assistant Builder's parse errors named the enum instead of its own
/// class, which left twelve messages untranslated.
/// </remarks>
[TestFixture]
public sealed class TranslationWrapperTests
{
    [Test]
    public void EveryTBWrapperNamesItsOwnClass()
    {
        var appDirectory = AppSourceDirectory();
        var wrappers = 0;
        var mismatches = new List<string>();
        var files = Directory.EnumerateFiles(appDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(Path.GetRelativePath(appDirectory, path)));

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            if (!source.Contains("TB(", StringComparison.Ordinal))
                continue;

            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            foreach (var wrapper in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.Identifier.Text == "TB"))
            {
                wrappers++;
                var owner = wrapper.Ancestors().OfType<BaseTypeDeclarationSyntax>().Last().Identifier.Text;
                var named = NamedTypes(wrapper).ToList();
                if (named.Count == 0 || named.Any(name => name != owner))
                    mismatches.Add($"{Path.GetRelativePath(appDirectory, file)}: the TB of {owner} names {(named.Count == 0 ? "no class" : string.Join(", ", named))}");
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wrappers, Is.GreaterThan(100), "The test found too few TB wrappers to be looking at the source of the app.");
            Assert.That(mismatches, Is.Empty);
        }
    }

    /// <summary>
    /// The types a wrapper names to build its key, through typeof and nameof.
    /// </summary>
    private static IEnumerable<string> NamedTypes(MethodDeclarationSyntax wrapper)
    {
        foreach (var typeOf in wrapper.DescendantNodes().OfType<TypeOfExpressionSyntax>())
            yield return SimpleName(typeOf.Type);

        foreach (var invocation in wrapper.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not IdentifierNameSyntax { Identifier.Text: "nameof" })
                continue;

            switch (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    yield return memberAccess.Name.Identifier.Text;
                    break;

                case TypeSyntax type:
                    yield return SimpleName(type);
                    break;
            }
        }
    }

    private static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        SimpleNameSyntax simple => simple.Identifier.Text,
        _ => type.ToString(),
    };

    private static bool IsBuildOutput(string relativePath)
    {
        var firstDirectory = relativePath.Split(Path.DirectorySeparatorChar)[0];
        return firstDirectory is "bin" or "obj";
    }

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio"));
}
