using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;

using SourceCodeRules.StyleAnalyzers;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks that extension methods are declared in extension blocks, not with a this parameter.
/// </summary>
/// <remarks>
/// AI Studio declares all its extension members in extension blocks, one per receiver. MWAIS0016 refuses the
/// classic form while compiling.
/// </remarks>
[TestFixture]
public sealed class ClassicExtensionMethodAnalyzerTests
{
    [TestCase("public static string Label(this Meeting meeting) => meeting.Name;", "Label", TestName = "A public extension method")]
    [TestCase("private static string Label(this Meeting meeting) => meeting.Name;", "Label", TestName = "A private extension method")]
    [TestCase("public static string Label(this Meeting meeting, string prefix) => prefix + meeting.Name;", "Label", TestName = "An extension method with further parameters")]
    [TestCase("public static T Echo<T>(this T value) where T : class => value;", "Echo", TestName = "A generic extension method")]
    [TestCase("public static void Bump(this ref int value) => value++;", "Bump", TestName = "An extension method with a ref receiver")]
    [TestCase("public static System.Threading.Tasks.Task<string> LabelAsync(this Meeting meeting) => System.Threading.Tasks.Task.FromResult(meeting.Name);", "LabelAsync", TestName = "An asynchronous extension method")]
    [TestCase("public static string Label(this Meeting meeting, params string[] prefixes) => string.Concat(prefixes) + meeting.Name;", "Label", TestName = "An extension method with a params parameter")]
    public async Task AClassicExtensionMethodIsRefused(string member, string name)
    {
        var reported = await AnalyzeAsync(member);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0016"));
            Assert.That(reported.FirstOrDefault()?.GetMessage(), Does.Contain($"'{name}'"));
        }
    }

    [TestCase("extension(Meeting meeting) { public string Label() => meeting.Name; }", TestName = "A member of an extension block")]
    [TestCase("extension<T>(T value) where T : class { public T Echo() => value; }", TestName = "A member of a generic extension block")]
    [TestCase("public static string Label(Meeting meeting) => meeting.Name;", TestName = "A static method without a this parameter")]
    public async Task AnythingElseIsLeftAlone(string member)
    {
        var reported = await AnalyzeAsync(member);

        Assert.That(reported, Is.Empty);
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string member)
    {
        var source =
            $$"""
              namespace Sample;

              public sealed class Meeting
              {
                  public string Name { get; set; } = "Kickoff";
              }

              public static class MeetingExtensions
              {
                  {{member}}
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new ClassicExtensionMethodAnalyzer());
    }
}
