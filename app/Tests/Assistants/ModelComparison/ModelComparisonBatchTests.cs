using AIStudio.Assistants.ModelComparison;

namespace AIStudio.Tests.Assistants.ModelComparison;

/// <summary>
/// Checks how the user goes through the runs of a batch, and how its votes and judge verdicts are counted.
/// </summary>
/// <remarks>
/// The counts have to survive the presentation order changing from run to run: a model which won column A in one run
/// and column B in the next must still be counted as the same winner both times.
/// </remarks>
[TestFixture]
public sealed class ModelComparisonBatchTests
{
    [Test]
    public void ARunWaitsUntilTheUserIsThroughWithTheOnesBefore()
    {
        var batch = new ModelComparisonBatch(3);
        batch.Add(Entry());
        batch.Add(Entry());

        Assert.That(batch.CurrentEntry, Is.SameAs(batch.Entries[0]));
    }

    [Test]
    public void AVoteMovesOnToTheNextRun()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry());
        batch.Add(Entry());

        var voteTaken = batch.Vote(ModelComparisonVote.COLUMN_A);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(voteTaken, Is.True);
            Assert.That(batch.CurrentEntry, Is.SameAs(batch.Entries[1]));
            Assert.That(batch.VoteChoiceAt(0), Is.EqualTo(ModelComparisonModelChoice.FIRST));
            Assert.That(batch.VoteChoiceAt(1), Is.Null, "The next run is still to be voted on.");
            Assert.That(batch.IsLastEntry, Is.True);
        }
    }

    [Test]
    public void ARunWithoutBothAnswersHasNothingToVoteOn()
    {
        var batch = new ModelComparisonBatch(1);
        batch.Add(EntryWithoutAnAnswer());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.Vote(ModelComparisonVote.TIE), Is.False);
            Assert.That(batch.CurrentIndex, Is.Zero);
            Assert.That(batch.VoteChoiceAt(0), Is.Null);
        }
    }

    [Test]
    public void ARunWithBothAnswersIsLeftOnlyByAVoteOrASkip()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.GoToNextEntry(), Is.False);
            Assert.That(batch.CurrentIndex, Is.Zero);
        }
    }

    [Test]
    public void ARunWithNothingToVoteOnCanBeLeftRightAway()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(EntryWithoutAnAnswer());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.GoToNextEntry(), Is.True);
            Assert.That(batch.CurrentIndex, Is.EqualTo(1));
        }
    }

    [Test]
    public void TheLastRunEndsTheBatch()
    {
        var batch = new ModelComparisonBatch(1);
        batch.Add(Entry());
        batch.Vote(ModelComparisonVote.TIE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.IsFinished, Is.True);
            Assert.That(batch.CurrentEntry, Is.Null);
        }
    }

    [Test]
    public void SkippedVotesEndTheBatchOnceTheLastRunArrived()
    {
        var batch = new ModelComparisonBatch(3);
        batch.Add(Entry());

        batch.SkipRemainingVotes(runsStillArriving: true);
        var finishedWhileRunsArrive = batch.IsFinished;

        batch.Add(Entry());
        batch.Add(Entry());
        batch.Settle(canceled: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(finishedWhileRunsArrive, Is.False);
            Assert.That(batch.IsFinished, Is.True);
        }
    }

    [Test]
    public void SkippedVotesEndASettledBatchRightAway()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry());
        batch.Add(Entry());

        batch.SkipRemainingVotes(runsStillArriving: false);

        Assert.That(batch.IsFinished, Is.True);
    }

    [Test]
    public void AStoppedBatchGoesStraightToTheSummary()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry());
        batch.Add(EntryWithoutAnAnswer());

        batch.Settle(canceled: true);

        Assert.That(batch.IsFinished, Is.True);
    }

    [Test]
    public void ABatchWhichWasNeitherStoppedNorSkippedWaitsForTheVotes()
    {
        var batch = new ModelComparisonBatch(1);
        batch.Add(Entry());

        batch.Settle(canceled: false);

        Assert.That(batch.IsFinished, Is.False);
    }

    [Test]
    public void TheStatusFollowsTheRun()
    {
        // Two runs arrived, the next ones ask the models, and the rest wait for a free slot:
        var batch = new ModelComparisonBatch(2 + ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS + 1);
        batch.Add(Entry());
        batch.Add(EntryWithoutAnAnswer());

        var statusBeforeTheVote = batch.StatusOf(0);
        batch.Vote(ModelComparisonVote.COLUMN_A);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(statusBeforeTheVote, Is.EqualTo(ModelComparisonRunStatus.AWAITING_VOTE));
            Assert.That(batch.StatusOf(0), Is.EqualTo(ModelComparisonRunStatus.VOTED));
            Assert.That(batch.StatusOf(1), Is.EqualTo(ModelComparisonRunStatus.NO_USABLE_ANSWER));
            for (var index = 2; index < 2 + ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS; index++)
                Assert.That(batch.StatusOf(index), Is.EqualTo(ModelComparisonRunStatus.RUNNING), $"Run {index + 1} asks the models.");

            Assert.That(batch.StatusOf(batch.RunCount - 1), Is.EqualTo(ModelComparisonRunStatus.QUEUED));
        }
    }

    [Test]
    public void ARunTheUserMovedPastWithoutAVoteIsSkipped()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry());
        batch.Add(Entry());

        batch.SkipThisRun();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.StatusOf(0), Is.EqualTo(ModelComparisonRunStatus.VOTE_SKIPPED));
            Assert.That(batch.StatusOf(1), Is.EqualTo(ModelComparisonRunStatus.AWAITING_VOTE));
        }
    }

    [Test]
    public void VotesAreCountedByModelNotByColumn()
    {
        // The first model stands in column A twice and in column B once, and the user votes for it every time:
        var batch = new ModelComparisonBatch(3);
        VoteOn(batch, Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST), ModelComparisonVote.COLUMN_A);
        VoteOn(batch, Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST), ModelComparisonVote.COLUMN_B);
        VoteOn(batch, Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST), ModelComparisonVote.COLUMN_A);

        var tally = batch.TallyVotes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.EqualTo(3));
            Assert.That(tally.FirstModelWins, Is.EqualTo(3));
            Assert.That(tally.SecondModelWins, Is.Zero);
        }
    }

    [Test]
    public void ARunWithoutAVoteIsNotCounted()
    {
        var batch = new ModelComparisonBatch(2);
        VoteOn(batch, Entry(), ModelComparisonVote.COLUMN_A);
        batch.Add(Entry());
        batch.SkipThisRun();

        var tally = batch.TallyVotes();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.EqualTo(2), "Both runs happened.");
            Assert.That(tally.ScoredRunCount, Is.EqualTo(1), "Only one run was voted on.");
            Assert.That(tally.FirstModelWins, Is.EqualTo(1));
        }
    }

    [Test]
    public void JudgeVerdictsAreCountedByModelNotByColumn()
    {
        // The first model stands in column A twice and in column B once, and the judge prefers it every time:
        var batch = new ModelComparisonBatch(3);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, Verdict(ModelComparisonVote.COLUMN_A)));
        batch.Add(Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, Verdict(ModelComparisonVote.COLUMN_B)));
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, Verdict(ModelComparisonVote.COLUMN_A)));

        var tally = batch.TallyJudgeVerdicts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batch.JudgeChoiceAt(1), Is.EqualTo(ModelComparisonModelChoice.FIRST), "Column B of the second run is the first model.");
            Assert.That(tally.ScoredRunCount, Is.EqualTo(3));
            Assert.That(tally.FirstModelWins, Is.EqualTo(3));
            Assert.That(tally.SecondModelWins, Is.Zero);
        }
    }

    [Test]
    public void AJudgeTieIsATieInEitherOrder()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, Verdict(ModelComparisonVote.TIE)));
        batch.Add(Entry(ModelComparisonPresentationOrder.SECOND_MODEL_FIRST, Verdict(ModelComparisonVote.TIE)));

        Assert.That(batch.TallyJudgeVerdicts().Ties, Is.EqualTo(2));
    }

    [Test]
    public void ARunWithoutAUsableJudgeVerdictIsNotCounted()
    {
        var batch = new ModelComparisonBatch(2);
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, Verdict(ModelComparisonVote.COLUMN_A)));
        batch.Add(Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST, new ModelComparisonJudgeVerdict { Label = "Judge", Completed = false }));

        var tally = batch.TallyJudgeVerdicts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tally.TotalRunCount, Is.EqualTo(2), "Both runs happened.");
            Assert.That(tally.ScoredRunCount, Is.EqualTo(1), "Only one run had a usable verdict.");
            Assert.That(batch.HasJudgeVerdict(0), Is.True);
            Assert.That(batch.HasJudgeVerdict(1), Is.False);
        }
    }

    [Test]
    public void ACopyDoesNotChangeWithTheBatch()
    {
        var batch = new ModelComparisonBatch(3);
        batch.Add(Entry());
        batch.SkipRemainingVotes(runsStillArriving: true);

        var copy = batch.Copy();
        batch.Vote(ModelComparisonVote.COLUMN_A);
        batch.Add(Entry());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.RunCount, Is.EqualTo(3));
            Assert.That(copy.Entries, Has.Count.EqualTo(1));
            Assert.That(copy.CurrentIndex, Is.Zero);
            Assert.That(copy.VoteChoiceAt(0), Is.Null);
            Assert.That(copy.SkipsRemainingVotes, Is.True);
        }
    }

    private static void VoteOn(ModelComparisonBatch batch, ModelComparisonBatchEntry entry, ModelComparisonVote vote)
    {
        batch.Add(entry);
        batch.Vote(vote);
    }

    private static ModelComparisonBatchEntry Entry() => Entry(ModelComparisonPresentationOrder.FIRST_MODEL_FIRST);

    private static ModelComparisonBatchEntry Entry(ModelComparisonPresentationOrder order, ModelComparisonJudgeVerdict? judge = null) => new()
    {
        PresentationOrder = order,
        Result = new ModelComparisonRunResult
        {
            First = Answer("First", true),
            Second = Answer("Second", true),
            Judge = judge,
        },
    };

    private static ModelComparisonBatchEntry EntryWithoutAnAnswer() => new()
    {
        PresentationOrder = ModelComparisonPresentationOrder.FIRST_MODEL_FIRST,
        Result = new ModelComparisonRunResult
        {
            First = Answer("First", true),
            Second = Answer("Second", false),
        },
    };

    private static ModelComparisonJudgeVerdict Verdict(ModelComparisonVote preferred) => new()
    {
        Label = "Judge",
        Completed = true,
        Preferred = preferred,
    };

    private static ModelComparisonAnswer Answer(string label, bool completed) => new()
    {
        Label = label,
        Text = completed ? $"The answer of {label}." : string.Empty,
        Completed = completed,
    };
}