using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks that the access checks run before anything else in the request pipeline of the app.
/// </summary>
/// <remarks>
/// A middleware or an endpoint which comes first would answer requests the checks never saw. The
/// test reads the calls from the source of Program.cs, so that a new one is checked without anybody
/// having to add it to a test.
/// </remarks>
[TestFixture]
public sealed class AppAccessPipelineTests
{
    private static string ReadProgramSource([CallerFilePath] string sourceFilePath = "") => File.ReadAllText(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "..", "MindWork AI Studio", "Program.cs"));

    [Test]
    public void TheAccessChecksComeBeforeEveryOtherMiddlewareAndEndpoint()
    {
        var calls = Regex.Matches(ReadProgramSource(), @"\bapp\.(Use|Map)\w*(<\w+>)?\(")
            .Select(match => match.Value)
            .ToList();

        Assert.That(calls, Has.Count.GreaterThan(2), "The test found no middleware or endpoint after the access checks.");
        Assert.That(calls.Take(2), Is.EqualTo(new[] { "app.UseHostFiltering(", "app.UseMiddleware<AppAccessMiddleware>(" }));
    }
}