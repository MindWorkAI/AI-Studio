using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks what the charts of the results have in common.
/// </summary>
[TestFixture]
public sealed class ModelComparisonChartsTests
{
    [TestCase(new double[0], 1)]
    [TestCase(new[] { 0.0 }, 1)]
    [TestCase(new[] { 5.0 }, 1)]
    [TestCase(new[] { 6.0 }, 2)]
    [TestCase(new[] { 3.0, 10.0 }, 2)]
    [TestCase(new[] { 11.0 }, 5)]
    [TestCase(new[] { 26.0 }, 10)]
    [TestCase(new[] { 0.4, 3000.0 }, 1000)]
    public void TheAxisStepsByOneTwoOrFiveTimesAPowerOfTen(double[] values, int expectedStep) => Assert.That(ModelComparisonCharts.AxisStep(values), Is.EqualTo(expectedStep));

    [TestCase(1, 0)]
    [TestCase(4, 0)]
    [TestCase(5, 30)]
    public void OnlyManyModelsTiltTheirNames(int modelCount, int expectedRotation) => Assert.That(ModelComparisonCharts.LabelRotation(modelCount), Is.EqualTo(expectedRotation));
}