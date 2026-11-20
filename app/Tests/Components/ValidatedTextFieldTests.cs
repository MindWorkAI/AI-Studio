using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AIStudio.Components;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks that every text field with a validation checks its text again while the user types.
/// </summary>
/// <remarks>
/// Without Immediate or a DebounceInterval, a MudTextField passes its text on only when the user leaves it, so an
/// issue stays visible while the user types the text which resolves it. A field with a DebounceInterval binds its
/// Value, not its Text: bound by its text, it would delay the texts its parent sets as well, e.g., by a reset, and
/// check them without having been touched, see TextFieldDefaults. A read-only field takes no typing and needs neither.
/// </remarks>
[TestFixture]
public sealed partial class ValidatedTextFieldTests
{
    private const string CHECKS_ON_LEAVING = "checks its text only when the user leaves it";
    private const string DEBOUNCED_BY_TEXT = "binds its text, so it delays and checks the texts its parent sets as well";

    [TestCase("""<MudTextField T="string" @bind-Value="@this.input" Validation="@this.Validate" DebounceInterval="@TextFieldDefaults.VALIDATION_DEBOUNCE_MILLISECONDS"/>""", ExpectedResult = "")]
    [TestCase("""<MudTextField T="string" @bind-Text="@this.input" Validation="@this.Validate" Immediate="@true"/>""", ExpectedResult = "")]
    [TestCase("""<MudTextField T="string" Text="@this.input" ReadOnly="@true" Validation="@this.Validate"/>""", ExpectedResult = "")]
    [TestCase("""<MudTextField T="string" @bind-Text="@this.input" Label="@T("Name")"/>""", ExpectedResult = "")]
    [TestCase("""<MudTextField T="string" @bind-Text="@this.input" Validation="@this.Validate"/>""", ExpectedResult = "1: " + CHECKS_ON_LEAVING)]
    [TestCase("""<MudTextField T="string" @bind-Value="@this.input" Validation="@this.Validate" Immediate="@false"/>""", ExpectedResult = "1: " + CHECKS_ON_LEAVING)]
    [TestCase("""<MudTextField T="string" @bind-Text="@this.input" Validation="@this.Validate" DebounceInterval="500"/>""", ExpectedResult = "1: " + DEBOUNCED_BY_TEXT)]
    [TestCase("""<MudTextField T="string" Text="@this.input" TextChanged="@this.Set" Validation="@this.Validate" DebounceInterval="500"/>""", ExpectedResult = "1: " + DEBOUNCED_BY_TEXT)]
    [TestCase("""<MudTextField T="string" Value="@this.input" ValueChanged="@(value => this.SetAsync(value))" OnKeyUp="() => this.Changed()" Sizing="@(this.MaxLines > this.Lines ? InputSizing.Auto : InputSizing.Fixed)" Label="@T("Name (a > b)")" Validation="@this.Validate"/>""", ExpectedResult = "1: " + CHECKS_ON_LEAVING)]
    [TestCase("""
              <MudTextField T="string"
                            @bind-Value="@this.first"
                            Validation="@this.Validate"
                            DebounceInterval="@TextFieldDefaults.VALIDATION_DEBOUNCE_MILLISECONDS"/>
              <MudTextFieldExtended T="string" @bind-Text="@this.other" Validation="@this.Validate"/>
              <MudTextField T="string" @bind-Text="@this.second" Validation="@this.Validate"/>
              """, ExpectedResult = "6: " + CHECKS_ON_LEAVING)]
    public string AValidatedTextFieldIsJudgedByHowItChecksWhileTheUserTypes(string razor) => string.Join(" | ", ValidatedTextFields(razor).SelectMany(field => IssuesOf(field).Select(issue => $"{field.Line}: {issue}")));

    [Test]
    public void EveryValidatedTextFieldChecksWhileTheUserTypes()
    {
        var appDirectory = AppSourceDirectory();
        var fields = 0;
        var issues = new List<string>();
        foreach (var file in RazorFiles(appDirectory))
        {
            foreach (var field in ValidatedTextFields(File.ReadAllText(file)))
            {
                fields++;
                issues.AddRange(IssuesOf(field).Select(issue => $"{Path.GetRelativePath(appDirectory, file)}:{field.Line}: {issue}"));
            }
        }

        Assert.That(fields, Is.GreaterThan(50), "The test found too few text fields with a validation, so it probably misreads the Razor files.");
        Assert.That(issues, Is.Empty, $"Give these text fields DebounceInterval=\"@{nameof(TextFieldDefaults)}.{nameof(TextFieldDefaults.VALIDATION_DEBOUNCE_MILLISECONDS)}\" and bind their Value:\n{string.Join('\n', issues)}");
    }

    /// <summary>
    /// A MudTextField of a Razor source, from its opening angle bracket to its closing one.
    /// </summary>
    /// <param name="Line">The line on which the field starts, counted from 1.</param>
    /// <param name="Source">The markup of the field.</param>
    private sealed record TextFieldTag(int Line, string Source);

    /// <summary>
    /// Finds every MudTextField with a validation which takes typing, i.e., which is not read-only.
    /// </summary>
    private static IEnumerable<TextFieldTag> ValidatedTextFields(string razor)
    {
        foreach (Match start in TextFieldStartRegex().Matches(razor))
        {
            var tag = razor[start.Index..(TagEnd(razor, start.Index + start.Length) + 1)];
            if (!ValidationRegex().IsMatch(tag) || ReadOnlyRegex().IsMatch(tag))
                continue;

            yield return new TextFieldTag(razor[..start.Index].Count(character => character == '\n') + 1, tag);
        }
    }

    private static IEnumerable<string> IssuesOf(TextFieldTag field)
    {
        var isDebounced = DebounceRegex().IsMatch(field.Source);
        if (!isDebounced && !ImmediateRegex().IsMatch(field.Source))
            yield return CHECKS_ON_LEAVING;

        if (isDebounced && TextBindingRegex().IsMatch(field.Source))
            yield return DEBOUNCED_BY_TEXT;
    }

    /// <summary>
    /// Finds the angle bracket which closes a tag.
    /// </summary>
    /// <remarks>
    /// An attribute value may hold an angle bracket of its own, e.g., in a lambda or a comparison, and the C# code
    /// inside its parentheses may hold strings with quotes, as @T("…") does. So the search skips attribute values,
    /// counts parentheses, and skips the strings inside them.
    /// </remarks>
    private static int TagEnd(string razor, int position)
    {
        var depth = 0;
        var isInsideValue = false;
        for (; position < razor.Length; position++)
        {
            var character = razor[position];
            if (depth > 0 && character == '"')
            {
                position = StringEnd(razor, position);
                continue;
            }

            switch (character)
            {
                case '(' or '[' or '{':
                    depth++;
                    break;

                case ')' or ']' or '}':
                    depth--;
                    break;

                case '"' when depth == 0:
                    isInsideValue = !isInsideValue;
                    break;

                case '>' when depth == 0 && !isInsideValue:
                    return position;
            }
        }

        throw new InvalidOperationException("A MudTextField has no end.");
    }

    /// <summary>
    /// Finds the quote which closes a C# string, skipping escaped characters.
    /// </summary>
    private static int StringEnd(string razor, int openingQuote)
    {
        for (var position = openingQuote + 1; position < razor.Length; position++)
        {
            if (razor[position] == '\\')
                position++;
            else if (razor[position] == '"')
                return position;
        }

        throw new InvalidOperationException("A string inside a MudTextField has no end.");
    }

    private static IEnumerable<string> RazorFiles(string appDirectory) => Directory.EnumerateFiles(appDirectory, "*.razor", SearchOption.AllDirectories)
        .Where(path => Path.GetRelativePath(appDirectory, path).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj" or "wwwroot"));

    [GeneratedRegex(@"<MudTextField\b")]
    private static partial Regex TextFieldStartRegex();

    [GeneratedRegex(@"\sValidation=""")]
    private static partial Regex ValidationRegex();

    [GeneratedRegex(@"\sReadOnly=""@?true""")]
    private static partial Regex ReadOnlyRegex();

    [GeneratedRegex(@"\sImmediate=""(?!@?false"")")]
    private static partial Regex ImmediateRegex();

    [GeneratedRegex(@"\sDebounceInterval=""")]
    private static partial Regex DebounceRegex();

    [GeneratedRegex(@"(@bind-Text|\sText|\sTextChanged)=""")]
    private static partial Regex TextBindingRegex();

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio"));
}