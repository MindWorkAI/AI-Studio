using AIStudio.AIOperations;
using AIStudio.Provider;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks how the provider of an AI operation is chosen from the facts about its candidates.
/// </summary>
/// <remarks>
/// Each row names the candidates, the rules, and what has to come out: the chosen provider, why the preferred
/// one was not chosen, and why the first named fallback failed. The rows cover every issue and their order,
/// the order of the fallbacks, the minimum of the operation for fallbacks switched on and off, and the rules
/// which apply to every candidate. Names are written out in full where a short one would find a namespace
/// of this assembly first, see PreselectedProviderTests.
/// </remarks>
[TestFixture]
public sealed class AIOperationProviderChoiceTests
{
    private const string PREFERRED = "preferred";
    private const string FIRST = "first";
    private const string SECOND = "second";

    [TestCaseSource(nameof(Cases))]
    public void ChoosesTheExpectedProvider(Func<AIOperationProviderCandidate> preferred, Func<AIOperationProviderCandidate>[] fallbacks, AIOperationProviderRules rules, string? expectedProviderId, AIOperationProviderIssue expectedPreferredIssue, AIOperationProviderIssue expectedFallbackIssue)
    {
        var choice = AIOperationProviderChoice.Choose(preferred(), fallbacks.Select(fallback => fallback()).ToList(), rules);

        Assert.Multiple(() =>
        {
            if (expectedProviderId is null)
                Assert.That(choice.HasProvider, Is.False, "No candidate passes, so no provider may be chosen.");
            else
                Assert.That(choice.Provider.Id, Is.EqualTo(expectedProviderId));

            Assert.That(choice.PreferredIssue, Is.EqualTo(expectedPreferredIssue));
            Assert.That(choice.FallbackIssue, Is.EqualTo(expectedFallbackIssue));
        });
    }

    [Test]
    public void ReportsWhetherAFallbackWasChosen()
    {
        var rules = Rules();
        var preferredChoice = AIOperationProviderChoice.Choose(Candidate(PREFERRED)(), [Candidate(FIRST)()], rules);
        var fallbackChoice = AIOperationProviderChoice.Choose(AIOperationProviderCandidate.NOT_NAMED, [Candidate(FIRST)()], rules);
        var noChoice = AIOperationProviderChoice.Choose(AIOperationProviderCandidate.NOT_NAMED, [], rules);

        Assert.Multiple(() =>
        {
            Assert.That(preferredChoice.UsesFallback, Is.False);
            Assert.That(fallbackChoice.UsesFallback, Is.True);
            Assert.That(noChoice.UsesFallback, Is.False, "Without a provider, nothing was chosen, not even a fallback.");
        });
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        //
        // The preferred provider and each issue in turn:
        //
        yield return Row("PreferredWinsWhenItPasses",
            Candidate(PREFERRED), [Candidate(FIRST)], Rules(),
            PREFERRED, AIOperationProviderIssue.NONE, AIOperationProviderIssue.NONE);

        yield return Row("PreferredNotSetFallsBackWithoutAHint",
            NotNamed, [Candidate(FIRST)], Rules(),
            FIRST, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NONE);

        yield return Row("PreferredMissingFallsBack",
            Missing, [Candidate(FIRST)], Rules(),
            FIRST, AIOperationProviderIssue.MISSING, AIOperationProviderIssue.NONE);

        yield return Row("PreferredProhibitedFallsBack",
            Candidate(PREFERRED, isProhibited: true), [Candidate(FIRST)], Rules(),
            FIRST, AIOperationProviderIssue.PROHIBITED, AIOperationProviderIssue.NONE);

        yield return Row("PreferredBelowTheGlobalMinimumFallsBack",
            Candidate(PREFERRED, ConfidenceLevel.LOW), [Candidate(FIRST)], Rules(global: ConfidenceLevel.MEDIUM),
            FIRST, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE, AIOperationProviderIssue.NONE);

        yield return Row("PreferredBelowTheOwnMinimumFallsBack",
            Candidate(PREFERRED, ConfidenceLevel.LOW), [Candidate(FIRST)], Rules(own: ConfidenceLevel.MEDIUM),
            FIRST, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE, AIOperationProviderIssue.NONE);

        yield return Row("PreferredBelowWhatTheContentRequiresFallsBack",
            Candidate(PREFERRED, ConfidenceLevel.LOW), [Candidate(FIRST)], Rules(required: ConfidenceLevel.MEDIUM),
            FIRST, AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT, AIOperationProviderIssue.NONE);

        yield return Row("PreferredRefusedForItsContentByTheCallerFallsBack",
            Candidate(PREFERRED, callerIssue: AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT), [Candidate(FIRST)], Rules(),
            FIRST, AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT, AIOperationProviderIssue.NONE);

        yield return Row("PreferredWithTooSmallAWindowFallsBack",
            Candidate(PREFERRED, callerIssue: AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL), [Candidate(FIRST)], Rules(),
            FIRST, AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL, AIOperationProviderIssue.NONE);

        yield return Row("ReachingTheMinimumExactlyIsEnough",
            Candidate(PREFERRED, ConfidenceLevel.MEDIUM), [], Rules(ConfidenceLevel.MEDIUM, ConfidenceLevel.MEDIUM, ConfidenceLevel.MEDIUM),
            PREFERRED, AIOperationProviderIssue.NONE, AIOperationProviderIssue.NONE);

        //
        // The order of the issues: a candidate reports the first rule it breaks.
        //
        yield return Row("AProhibitionComesBeforeTheConfidence",
            Candidate(PREFERRED, ConfidenceLevel.LOW, isProhibited: true), [], Rules(global: ConfidenceLevel.HIGH),
            null, AIOperationProviderIssue.PROHIBITED, AIOperationProviderIssue.NOT_SET);

        yield return Row("TheMinimumComesBeforeWhatTheContentRequires",
            Candidate(PREFERRED, ConfidenceLevel.LOW), [], Rules(own: ConfidenceLevel.MEDIUM, required: ConfidenceLevel.HIGH),
            null, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE, AIOperationProviderIssue.NOT_SET);

        yield return Row("WhatTheContentRequiresComesBeforeTheCheckOfTheCaller",
            Candidate(PREFERRED, ConfidenceLevel.LOW, callerIssue: AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL), [], Rules(required: ConfidenceLevel.MEDIUM),
            null, AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT, AIOperationProviderIssue.NOT_SET);

        //
        // The order of the fallbacks:
        //
        yield return Row("TheFirstPassingFallbackWins",
            NotNamed, [Candidate(FIRST, isProhibited: true), Candidate(SECOND), Candidate("third")], Rules(),
            SECOND, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NONE);

        yield return Row("AFallbackNobodyNamedIsSkipped",
            NotNamed, [NotNamed, Candidate(SECOND)], Rules(),
            SECOND, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NONE);

        //
        // The minimum of the operation for fallbacks:
        //
        yield return Row("TheOwnMinimumAppliesToFallbacksWhenSwitchedOn",
            NotNamed, [Candidate(FIRST, ConfidenceLevel.LOW)], Rules(own: ConfidenceLevel.MEDIUM),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE);

        yield return Row("TheOwnMinimumSparesFallbacksWhenSwitchedOff",
            NotNamed, [Candidate(FIRST, ConfidenceLevel.LOW)], Rules(own: ConfidenceLevel.MEDIUM, ownForFallbacks: false),
            FIRST, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NONE);

        yield return Row("TheOwnMinimumStillAppliesToThePreferredWhenSwitchedOffForFallbacks",
            Candidate(PREFERRED, ConfidenceLevel.LOW), [Candidate(FIRST, ConfidenceLevel.LOW)], Rules(own: ConfidenceLevel.MEDIUM, ownForFallbacks: false),
            FIRST, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE, AIOperationProviderIssue.NONE);

        //
        // What applies to every candidate, even with the minimum of the operation switched off for fallbacks:
        //
        yield return Row("TheGlobalMinimumAppliesToFallbacks",
            NotNamed, [Candidate(FIRST, ConfidenceLevel.LOW)], Rules(global: ConfidenceLevel.MEDIUM, ownForFallbacks: false),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE);

        yield return Row("AProhibitionAppliesToFallbacks",
            NotNamed, [Candidate(FIRST, isProhibited: true)], Rules(ownForFallbacks: false),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.PROHIBITED);

        yield return Row("WhatTheContentRequiresAppliesToFallbacks",
            NotNamed, [Candidate(FIRST, ConfidenceLevel.LOW)], Rules(required: ConfidenceLevel.MEDIUM, ownForFallbacks: false),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT);

        yield return Row("TheCheckOfTheCallerAppliesToFallbacks",
            NotNamed, [Candidate(FIRST, callerIssue: AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL)], Rules(ownForFallbacks: false),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL);

        //
        // What is reported when nothing passes:
        //
        yield return Row("NothingNamedNeedsNoHint",
            NotNamed, [], Rules(),
            null, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NOT_SET);

        yield return Row("TheFallbackIssueIsTheOneOfTheFirstNamedFallback",
            Candidate(PREFERRED, isProhibited: true), [NotNamed, Missing, Candidate(SECOND, isProhibited: true)], Rules(),
            null, AIOperationProviderIssue.PROHIBITED, AIOperationProviderIssue.MISSING);

        yield return Row("FallbacksNobodyNamedLeaveTheFallbackIssueNotSet",
            Candidate(PREFERRED, isProhibited: true), [NotNamed, NotNamed], Rules(),
            null, AIOperationProviderIssue.PROHIBITED, AIOperationProviderIssue.NOT_SET);
    }

    private static TestCaseData Row(string name, Func<AIOperationProviderCandidate> preferred, Func<AIOperationProviderCandidate>[] fallbacks, AIOperationProviderRules rules, string? expectedProviderId, AIOperationProviderIssue expectedPreferredIssue, AIOperationProviderIssue expectedFallbackIssue) => new TestCaseData(preferred, fallbacks, rules, expectedProviderId, expectedPreferredIssue, expectedFallbackIssue).SetName(name);

    private static AIOperationProviderRules Rules(ConfidenceLevel global = ConfidenceLevel.NONE, ConfidenceLevel own = ConfidenceLevel.NONE, ConfidenceLevel required = ConfidenceLevel.NONE, bool ownForFallbacks = true) => new(global, own, required, ownForFallbacks);

    //
    // The rows hand over factories instead of candidates: NUnit gathers the rows before TestHost sets up the
    // logger factory, and a provider built that early breaks the type initializer of Settings.Provider for
    // every test of the run.
    //
    private static Func<AIOperationProviderCandidate> NotNamed => () => AIOperationProviderCandidate.NOT_NAMED;

    private static Func<AIOperationProviderCandidate> Missing => () => AIOperationProviderCandidate.MISSING;

    private static Func<AIOperationProviderCandidate> Candidate(string id, ConfidenceLevel confidence = ConfidenceLevel.HIGH, bool isProhibited = false, AIOperationProviderIssue callerIssue = AIOperationProviderIssue.NONE) => () => new(new AIStudio.Settings.Provider(1, id, id, LLMProviders.SELF_HOSTED, new("test-model", null)), true, true, isProhibited, confidence, callerIssue);
}
