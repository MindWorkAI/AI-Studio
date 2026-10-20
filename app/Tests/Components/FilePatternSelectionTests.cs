using AIStudio.Components;
using AIStudio.Tools;

namespace AIStudio.Tests.Components;

public sealed class FilePatternSelectionTests
{
    [TestCase("")]
    [TestCase("*.mp3;draft-*")]
    [TestCase(FileExclusions.DEFAULT_PATTERNS)]
    public void ValidExclusionsArePermanentlyVisible(string patterns)
    {
        var component = new TestableFilePatternSelection();
        component.SetExclusions(patterns);

        Assert.That(component.HasExclusions, Is.True);
    }

    [Test]
    public void AParentRefreshKeepsTheExistingExclusionFieldVisible()
    {
        var component = new TestableFilePatternSelection();
        component.SetExclusions("*.mp3;draft-*");
        var field = component.ExcludedContent;

        component.RefreshParameters();
        Assert.Multiple(() =>
        {
            Assert.That(component.HasExclusions, Is.True);
            Assert.That(component.ExcludedContent, Is.SameAs(field), "A parent render must keep the existing field and its validation.");
        });
    }

    [TestCase(":text:")]
    [TestCase("src/bin/")]
    [TestCase("**/*.txt")]
    public void InvalidStoredExclusionsRemainVisible(string patterns)
    {
        var component = new TestableFilePatternSelection();
        component.SetExclusions(patterns);

        Assert.That(component.HasExclusions, Is.True);
    }

    [Test]
    public void InclusionOnlyCallersHaveNoExclusionSection()
    {
        var component = new TestableFilePatternSelection();
        component.RefreshParameters();

        Assert.Multiple(() =>
        {
            Assert.That(component.ExcludedContent, Is.Null);
            Assert.That(component.HasExclusions, Is.False);
        });
    }

    [Test]
    public void RemovingTheOptionalFieldRemovesTheExclusionSection()
    {
        var component = new TestableFilePatternSelection();
        component.SetExclusions("*.mp3");
        component.RemoveExclusions();

        Assert.That(component.HasExclusions, Is.False);
    }

    private sealed class TestableFilePatternSelection : FilePatternSelection
    {
        public void SetExclusions(string patterns)
        {
            this.ExcludedContent = builder => builder.AddContent(0, patterns);
            this.OnParametersSet();
        }

        public void RemoveExclusions()
        {
            this.ExcludedContent = null;
            this.OnParametersSet();
        }

        public void RefreshParameters() => this.OnParametersSet();
    }
}
