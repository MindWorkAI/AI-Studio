using System.Globalization;

using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that assistant plugins read numbers and write dates the same way on every computer.
/// </summary>
/// <remarks>
/// The app sets no culture of its own, so the current culture is the one of the operating system. Each case runs
/// under a culture which writes differently than the invariant one: de-DE with a decimal comma and German names of
/// months, sv-SE with a minus sign of its own (U+2212), fi-FI with a period between hours and minutes, and th-TH
/// with the years of the Buddhist era. Each case first checks that its culture really writes differently, so that
/// a system without the data of the cultures fails instead of passing.
/// </remarks>
[TestFixture]
public sealed class AssistantPluginCultureTests
{
    private static readonly DateTime MOMENT = new(2026, 3, 11, 14, 3, 22, 500);

    [TestCase("de-DE", 12.5, "12.5")]
    [TestCase("sv-SE", -3.5, "-3.5")]
    public void ANumberFromLuaBecomesTheSameTextOnEveryComputer(string culture, double number, string expected)
    {
        InCulture(culture, () =>
        {
            var props = new Dictionary<string, object> { ["PrefillText"] = number };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(number.ToString(CultureInfo.CurrentCulture), Is.Not.EqualTo(expected), "The culture must write the number differently, or this case checks nothing.");
                Assert.That(AssistantComponentPropHelper.ReadString(props, "PrefillText"), Is.EqualTo(expected));
            }
        });
    }

    [TestCase("de-DE")]
    [TestCase("sv-SE")]
    public void AWholeNumberIsReadOnEveryComputer(string culture)
    {
        InCulture(culture, () =>
        {
            var props = new Dictionary<string, object>
            {
                ["FromLua"] = -3.0,
                ["FromASetter"] = 7,
                ["AsText"] = "-12",
                ["WithAFraction"] = 6.5,
                ["TooLarge"] = 1e15
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(AssistantComponentPropHelper.ReadInt(props, "FromLua"), Is.EqualTo(-3));
                Assert.That(AssistantComponentPropHelper.ReadInt(props, "FromASetter"), Is.EqualTo(7));
                Assert.That(AssistantComponentPropHelper.ReadInt(props, "AsText"), Is.EqualTo(-12));
                Assert.That(AssistantComponentPropHelper.ReadInt(props, "WithAFraction", fallback: 42), Is.EqualTo(42));
                Assert.That(AssistantComponentPropHelper.ReadNullableInt(props, "TooLarge"), Is.Null);
                Assert.That(AssistantComponentPropHelper.ReadNullableInt(props, "Missing"), Is.Null);
            }
        });
    }

    [TestCase("fi-FI", "yyyy-MM-dd HH:mm:ss", "2026-03-11 14:03:22")]
    [TestCase("th-TH", "yyyy-MM-dd HH:mm:ss", "2026-03-11 14:03:22")]
    [TestCase("de-DE", "dd. MMMM yyyy", "11. March 2026")]
    public void TheDateTimeHelperFormatsTheSameOnEveryComputer(string culture, string format, string expected)
    {
        InCulture(culture, () =>
        {
            var table = PluginAssistants.CreateDateTimeTable(MOMENT, format);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(MOMENT.ToString(format, CultureInfo.CurrentCulture), Is.Not.EqualTo(expected), "The culture must write the moment differently, or this case checks nothing.");
                Assert.That(table["formatted"].Read<string>(), Is.EqualTo(expected));
                Assert.That(table["year"].Read<double>(), Is.EqualTo(2026));
                Assert.That(table["millisecond"].Read<double>(), Is.EqualTo(500));
            }
        });
    }

    private static void InCulture(string name, Action check)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            check();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}