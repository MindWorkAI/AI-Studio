using System.Reflection;

using AIStudio.Tools;

namespace AIStudio.Tests.Icons;

/// <summary>
/// Checks that every icon of the app is a glyph of the Lucide font, apart from the few that keep an icon of MudBlazor.
/// </summary>
/// <remarks>
/// AppIcons is the one place where the app picks its icons. A constant that takes its value from anywhere
/// but LucideIcons would bring back an icon of another set, which looks out of place next to all the others.
/// </remarks>
[TestFixture]
public sealed class AppIconsTests
{
    /// <summary>
    /// The icons that keep the SVG of MudBlazor: brand logos keep their own marks, and the numbered steps keep
    /// their numbers, because Lucide has no numbered icons.
    /// </summary>
    private static readonly HashSet<string> KEPT_MUDBLAZOR_ICONS =
    [
        nameof(AppIcons.GITHUB),
        nameof(AppIcons.STEP_1),
        nameof(AppIcons.STEP_2),
        nameof(AppIcons.STEP_3),
        nameof(AppIcons.STEP_4),
        nameof(AppIcons.STEP_5),
        nameof(AppIcons.STEP_6)
    ];

    [Test]
    public void EveryIconIsALucideGlyphOrAKeptMudBlazorIcon()
    {
        var lucideGlyphs = Constants(typeof(LucideIcons)).Values.ToHashSet(StringComparer.Ordinal);
        var foreign = Constants(typeof(AppIcons))
            .Where(icon => !KEPT_MUDBLAZOR_ICONS.Contains(icon.Key) && !lucideGlyphs.Contains(icon.Value))
            .Select(icon => icon.Key)
            .ToList();

        Assert.That(foreign, Is.Empty, "These icons are no glyph of the Lucide font.");
    }

    [Test]
    public void EveryKeptMudBlazorIconStaysAnSvg()
    {
        var icons = Constants(typeof(AppIcons));

        using (Assert.EnterMultipleScope())
        {
            foreach (var icon in KEPT_MUDBLAZOR_ICONS)
                Assert.That(icons[icon], Does.StartWith("<"), $"{icon} has to stay an SVG of MudBlazor.");
        }
    }

    private static Dictionary<string, string> Constants(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .ToDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!);
}