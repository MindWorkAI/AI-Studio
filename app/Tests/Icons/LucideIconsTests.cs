using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using AIStudio.Tools;

namespace AIStudio.Tests.Icons;

/// <summary>
/// Checks the Lucide icons the app was built with against the font it ships.
/// </summary>
/// <remarks>
/// LucideIconGeneratorTests runs the generator by hand. These tests ask whether it also ran while the app
/// was built, against the code points in the repository, and whether the font those code points belong to
/// ships with the app.
/// </remarks>
[TestFixture]
public sealed class LucideIconsTests
{
    private const string FONT_ICON_PREFIX = "lucide/";

    [Test]
    public void TheAppKnowsEveryIconOfTheShippedCodepointsUnderItsName()
    {
        var codepoints = ReadCodepoints();
        var wrong = codepoints
            .Where(entry => !LucideIconRegistry.TryGet(entry.Key, out var icon) || icon != FONT_ICON_PREFIX + char.ConvertFromUtf32(entry.Value))
            .Select(entry => entry.Key)
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(codepoints, Has.Count.GreaterThan(1000), "The code points of the font were not found or not read.");
            Assert.That(wrong, Is.Empty, "These icons are missing from the app or point to another glyph.");
            Assert.That(IconConstants(), Has.Count.EqualTo(codepoints.Count), "Every name, aliases included, gets a constant of its own.");
        }
    }

    [Test]
    public void EveryIconIsAFontIconOfOneGlyph()
    {
        var malformed = IconConstants()
            .Where(icon => !IsFontIconOfOneGlyph(icon.Value))
            .Select(icon => icon.Key)
            .ToList();

        Assert.That(malformed, Is.Empty);
    }

    [Test]
    public void TheRegistryFindsAnIconOnlyByItsExactName()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(LucideIconRegistry.TryGet("trash-2", out var trash), Is.True);
            Assert.That(trash, Is.EqualTo(LucideIcons.TRASH_2));

            Assert.That(LucideIconRegistry.TryGet("home", out var home), Is.True, "An alias is a name of its own.");
            Assert.That(home, Is.EqualTo(LucideIcons.HOUSE));

            Assert.That(LucideIconRegistry.TryGet("Trash-2", out _), Is.False);
            Assert.That(LucideIconRegistry.TryGet("trash 2", out _), Is.False);
            Assert.That(LucideIconRegistry.TryGet("lucide:trash-2", out _), Is.False, "The prefix of plugins is not part of the name.");
            Assert.That(LucideIconRegistry.TryGet(string.Empty, out _), Is.False);
        }
    }

    [Test]
    public void TheFontShipsWithTheAppAlongWithItsLicense()
    {
        var webRoot = Path.Combine(AppSourceDirectory(), "wwwroot");
        var css = File.ReadAllText(Path.Combine(webRoot, "app.css"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(Path.Combine(webRoot, "fonts", "lucide.woff2")), Is.True);
            Assert.That(File.Exists(Path.Combine(webRoot, "fonts", "LICENSE-lucide.txt")), Is.True);
            Assert.That(css, Does.Contain("url('fonts/lucide.woff2')"), "app.css has to declare the font.");
            Assert.That(css, Does.Contain(".mud-icon-root.lucide"), "app.css has to apply the font to the class every icon string names.");
        }
    }

    private static bool IsFontIconOfOneGlyph(string icon)
    {
        if (!icon.StartsWith(FONT_ICON_PREFIX, StringComparison.Ordinal))
            return false;

        var glyph = icon[FONT_ICON_PREFIX.Length..];
        var isOneCharacter = glyph.Length is 1 && !char.IsSurrogate(glyph[0]) || glyph.Length is 2 && char.IsSurrogatePair(glyph, 0);

        return isOneCharacter && CharUnicodeInfo.GetUnicodeCategory(glyph, 0) is UnicodeCategory.PrivateUse;
    }

    private static Dictionary<string, string> IconConstants() => typeof(LucideIcons)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);

    private static Dictionary<string, int> ReadCodepoints()
    {
        var path = Path.Combine(AppSourceDirectory(), "..", "SourceGeneratedMappings", "Lucide", "codepoints.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetInt32());
    }

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio"));
}