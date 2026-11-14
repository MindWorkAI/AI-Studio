using AIStudio.Assistants.ModelComparison;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks which preset the Answer Test of the Model Comparison assistant opens with.
/// </summary>
[TestFixture]
public sealed class ModelComparisonPresetChoiceTests
{
    private static readonly ModelComparisonPreset FIRST = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Alpha" };
    private static readonly ModelComparisonPreset SECOND = new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Beta" };

    [Test]
    public void ThePreselectedPresetComesFirst()
    {
        var options = new DataModelComparison { PreselectOptions = true, PreselectedPresetId = SECOND.Id.ToString("D") };

        Assert.That(ModelComparisonPresetChoice.Choose([FIRST, SECOND], options), Is.EqualTo(SECOND.Id));
    }

    [Test]
    public void WithoutOptionsThePreselectedPresetDoesNotCount()
    {
        var options = new DataModelComparison { PreselectOptions = false, PreselectedPresetId = SECOND.Id.ToString("D") };

        Assert.That(ModelComparisonPresetChoice.Choose([FIRST, SECOND], options), Is.EqualTo(FIRST.Id), "With the options switched off, the stored preset must not count.");
    }

    [Test]
    public void ADeletedPreselectedPresetFallsBackToTheFirstOne()
    {
        var options = new DataModelComparison { PreselectOptions = true, PreselectedPresetId = "33333333-3333-3333-3333-333333333333" };

        Assert.That(ModelComparisonPresetChoice.Choose([FIRST, SECOND], options), Is.EqualTo(FIRST.Id));
    }

    [Test]
    public void ThePresetIdCountsWhateverItsCase()
    {
        var options = new DataModelComparison { PreselectOptions = true, PreselectedPresetId = "BBBBBBBB-2222-2222-2222-222222222222" };
        var preset = new ModelComparisonPreset { Id = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"), Name = "Gamma" };

        Assert.That(ModelComparisonPresetChoice.Choose([FIRST, preset], options), Is.EqualTo(preset.Id));
    }

    [Test]
    public void WithoutPresetsANewOneIsNeeded()
    {
        var options = new DataModelComparison { PreselectOptions = true, PreselectedPresetId = SECOND.Id.ToString("D") };

        Assert.That(ModelComparisonPresetChoice.Choose([], options), Is.Null);
    }
}