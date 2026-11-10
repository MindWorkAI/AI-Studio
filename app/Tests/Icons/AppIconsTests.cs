using System.Reflection;

using AIStudio.Tools;

namespace AIStudio.Tests.Icons;

/// <summary>
/// Checks that every icon of the app is a glyph of the Lucide font, apart from the brand logos.
/// </summary>
/// <remarks>
/// AppIcons is the one place where the app picks its icons. A constant that takes its value from anywhere
/// but LucideIcons would bring back an icon of another set, which looks out of place next to all the others.
/// </remarks>
[TestFixture]
public sealed class AppIconsTests
{
    /// <summary>
    /// Brand logos keep their own marks instead of a Lucide icon.
    /// </summary>
    private static readonly HashSet<string> BRAND_LOGOS = [nameof(AppIcons.GITHUB)];

    [Test]
    public void EveryIconIsALucideGlyphOrABrandLogo()
    {
        var lucideGlyphs = Constants(typeof(LucideIcons)).Values.ToHashSet(StringComparer.Ordinal);
        var foreign = Constants(typeof(AppIcons))
            .Where(icon => !BRAND_LOGOS.Contains(icon.Key) && !lucideGlyphs.Contains(icon.Value))
            .Select(icon => icon.Key)
            .ToList();

        Assert.That(foreign, Is.Empty, "These icons are no glyph of the Lucide font.");
    }

    [Test]
    public void EveryBrandLogoStaysTheMarkOfItsBrand()
    {
        var icons = Constants(typeof(AppIcons));

        using (Assert.EnterMultipleScope())
        {
            foreach (var logo in BRAND_LOGOS)
                Assert.That(icons[logo], Does.StartWith("<"), $"{logo} has to stay an SVG logo.");
        }
    }

    private static Dictionary<string, string> Constants(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
}