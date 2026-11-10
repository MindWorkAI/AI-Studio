using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which preparation the BeyondFacts Lab shows after its list changed.
/// </summary>
[TestFixture]
public sealed class BflProjectSelectionTests
{
    private static readonly BflProjectEntry CHANGED_LAST = Entry("Changed last");
    private static readonly BflProjectEntry SELECTED_LAST = Entry("Selected last");
    private static readonly BflProjectEntry JUST_CREATED = Entry("Just created");
    private static readonly IReadOnlyList<BflProjectEntry> PROJECTS = [CHANGED_LAST, SELECTED_LAST, JUST_CREATED];

    [Test]
    public void ThePreparationAskedForComesFirst()
    {
        var chosen = BflProjectSelection.Choose(PROJECTS, JUST_CREATED.ProjectId, SELECTED_LAST.ProjectId);

        Assert.That(chosen, Is.SameAs(JUST_CREATED));
    }

    [Test]
    public void WithoutAWishThePreparationSelectedLastIsShown()
    {
        var chosen = BflProjectSelection.Choose(PROJECTS, null, SELECTED_LAST.ProjectId);

        Assert.That(chosen, Is.SameAs(SELECTED_LAST));
    }

    [Test]
    public void APreparationWhichIsGoneFallsBackToTheOneSelectedLast()
    {
        var chosen = BflProjectSelection.Choose(PROJECTS, Guid.NewGuid(), SELECTED_LAST.ProjectId);

        Assert.That(chosen, Is.SameAs(SELECTED_LAST));
    }

    [Test]
    public void WithoutAnyKnownSelectionThePreparationChangedLastIsShown()
    {
        var chosen = BflProjectSelection.Choose(PROJECTS, Guid.NewGuid(), Guid.NewGuid());

        Assert.That(chosen, Is.SameAs(CHANGED_LAST));
    }

    [Test]
    public void WithoutPreparationsNothingIsShown()
    {
        var chosen = BflProjectSelection.Choose([], Guid.NewGuid(), null);

        Assert.That(chosen, Is.Null);
    }

    [Test]
    public void APreparationWhichCannotBeOpenedCanStillBeShown()
    {
        var unavailable = new BflProjectEntry(Guid.NewGuid(), string.Empty, DateTimeOffset.UtcNow, BflProjectLoadStatus.UNAVAILABLE, null);

        var chosen = BflProjectSelection.Choose([CHANGED_LAST, unavailable], unavailable.ProjectId, null);

        Assert.That(chosen, Is.SameAs(unavailable));
    }

    private static BflProjectEntry Entry(string name)
    {
        var project = new BflProject
        {
            ProjectId = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ModifiedAtUtc = DateTimeOffset.UtcNow,
        };

        return BflProjectEntry.FromProject(project);
    }
}