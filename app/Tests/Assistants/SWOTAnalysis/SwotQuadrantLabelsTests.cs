using AIStudio.Assistants.SWOTAnalysis;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotQuadrantLabelsTests
{
    private static readonly SwotQuadrantLabels LABELS = new("Stärken", "Schwächen", "Chancen", "Risiken", "Intern", "Extern", "Positiv", "Negativ");

    [TestCase(SwotAnalysisCategory.STRENGTHS, "Stärken", "Intern · Positiv")]
    [TestCase(SwotAnalysisCategory.WEAKNESSES, "Schwächen", "Intern · Negativ")]
    [TestCase(SwotAnalysisCategory.OPPORTUNITIES, "Chancen", "Extern · Positiv")]
    [TestCase(SwotAnalysisCategory.THREATS, "Risiken", "Extern · Negativ")]
    public void EveryQuadrantNamesItselfAndWhereItLies(SwotAnalysisCategory category, string expectedName, string expectedAxes)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(LABELS.NameOf(category), Is.EqualTo(expectedName));
            Assert.That(LABELS.AxesOf(category), Is.EqualTo(expectedAxes));
        }
    }
}