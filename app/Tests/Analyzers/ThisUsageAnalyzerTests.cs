using AIStudio.Tests.Models.Generation;

using Microsoft.CodeAnalysis;

using SourceCodeRules.UsageAnalyzers;

namespace AIStudio.Tests.Analyzers;

/// <summary>
/// Checks which members MWAIS0005 wants to be reached through <c>this.</c>, and that extension methods are never
/// asked for it.
/// </summary>
/// <remarks>
/// An extension member has no <c>this</c>: a classic extension method is static, and in an extension block C#
/// refuses the keyword. There, every instance member is reached through the name of the receiver, and a sibling
/// member without it does not even compile. Should a later version of C# reach the receiver without its name,
/// the analyzer would demand a <c>this.</c> which nobody can write, and the tests for extension blocks fail.
///
/// The same holds for the keyword <c>field</c> of a property: its backing field has no name which could follow
/// <c>this.</c>, while every other member next to it still needs one.
/// </remarks>
[TestFixture]
public sealed class ThisUsageAnalyzerTests
{
    [TestCase("this.location", TestName = "A field through this")]
    [TestCase("this.Name", TestName = "A property through this")]
    [TestCase("this.Describe()", TestName = "A method through this")]
    [TestCase("CITY", TestName = "A static field")]
    [TestCase("ROOM", TestName = "A constant")]
    [TestCase("Shorten(this.location)", TestName = "A static method")]
    public async Task AMemberWhichNeedsNoThisIsLeftAlone(string expression)
    {
        var reported = await AnalyzeClassAsync(expression);

        Assert.That(reported, Is.Empty);
    }

    [TestCase("location", TestName = "A field without this")]
    [TestCase("Name", TestName = "A property without this")]
    [TestCase("Describe()", TestName = "A method without this")]
    public async Task AnInstanceMemberWithoutThisIsRefused(string expression)
    {
        var reported = await AnalyzeClassAsync(expression);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0005"));
        }
    }

    [TestCase("get => field ?? this.Name; set => field = value;", TestName = "A getter and a setter which reach the backing field")]
    [TestCase("get; set => field = value.Trim();", TestName = "A setter which reaches the backing field")]
    [TestCase("get => field ??= this.Describe();", TestName = "A getter which fills the backing field on first use")]
    public async Task TheBackingFieldOfAPropertyIsLeftAlone(string accessors)
    {
        var reported = await AnalyzePropertyAsync(accessors);

        Assert.That(reported, Is.Empty);
    }

    [Test]
    public async Task AMemberNextToTheBackingFieldStillNeedsThis()
    {
        var reported = await AnalyzePropertyAsync("get => field ?? Name; set => field = value;");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(reported.FirstOrDefault()?.Id, Is.EqualTo("MWAIS0005"));
        }
    }

    [Test]
    public async Task AClassicExtensionMethodIsLeftAlone()
    {
        var source =
            """
            namespace Sample;

            public sealed class Meeting
            {
                public string Name { get; set; } = "Kickoff";
            }

            public static class MeetingExtensions
            {
                private static readonly string PREFIX = "Meeting: ";

                public static string Label(this Meeting meeting) => PREFIX + meeting.Name;

                public static string Title(this Meeting meeting) => Label(meeting).ToUpperInvariant();
            }
            """;

        var reported = await CompilationHarness.AnalyzeAsync(source, new ThisUsageAnalyzer());

        Assert.That(reported, Is.Empty);
    }

    [TestCase("public string Label() => meeting.Name;", TestName = "A member of the receiver")]
    [TestCase("public string Label() => meeting.Title();", TestName = "A sibling through the receiver")]
    [TestCase("public string Label() => Title(meeting);", TestName = "A sibling as a static method")]
    [TestCase("""public string Label() => PREFIX + SUFFIX + TB("Meeting");""", TestName = "The static members of the class")]
    [TestCase("public string Label() { System.Func<string> title = () => meeting.Title(); return title(); }", TestName = "A lambda")]
    [TestCase("public string Label() { string Title() => meeting.Name; return Title(); }", TestName = "A local function")]
    [TestCase("public string Label => meeting.Title();", TestName = "An extension property")]
    [TestCase("public static Meeting Create() => new(); public static Meeting Again() => Create();", TestName = "A static member of the block")]
    public async Task AMemberOfAnExtensionBlockIsLeftAlone(string member)
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
                  private const string SUFFIX = ".";

                  private static readonly string PREFIX = "Meeting: ";

                  private static string TB(string fallbackEN) => fallbackEN;

                  extension(Meeting meeting)
                  {
                      public string Title() => meeting.Name;

                      {{member}}
                  }
              }
              """;

        var reported = await CompilationHarness.AnalyzeAsync(source, new ThisUsageAnalyzer());

        Assert.That(reported, Is.Empty);
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeClassAsync(string expression)
    {
        var source =
            $$"""
              namespace Sample;

              public sealed class Meeting
              {
                  private const string ROOM = "A";

                  private static readonly string CITY = "Cologne";

                  private readonly string location = "Town hall";

                  public string Name { get; set; } = "Kickoff";

                  public string Run() => {{expression}};

                  private string Describe() => this.Name;

                  private static string Shorten(string text) => text[..1];
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new ThisUsageAnalyzer());
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzePropertyAsync(string accessors)
    {
        var source =
            $$"""
              namespace Sample;

              public sealed class Meeting
              {
                  public string Name { get; set; } = "Kickoff";

                  public string Title { {{accessors}} }

                  private string Describe() => this.Name;
              }
              """;

        return await CompilationHarness.AnalyzeAsync(source, new ThisUsageAnalyzer());
    }
}
