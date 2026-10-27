using AIStudio.Assistants.SWOTAnalysis;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotRepairRoundTests
{
    private const string VALID_ANSWER = """{"findings": [{"summary": "Experienced team", "explanation": "The documents describe long employee tenure."}]}""";
    private const string ANSWER_WITHOUT_EXPLANATION = """{"findings": [{"summary": "Experienced team", "explanation": ""}]}""";
    private const string ANSWER_WITHOUT_JSON = "I cannot analyze this category.";

    [Test]
    public void TheRepairRequestNamesTheIssue()
    {
        var request = SwotModelAnswer.RepairRequestOf("Finding 1 has no explanation.");

        Assert.Multiple(() =>
        {
            Assert.That(request, Does.Contain("Finding 1 has no explanation."));
            Assert.That(request, Does.Contain("complete corrected JSON object"));
        });
    }

    [Test]
    public async Task AReadableAnswerNeedsNoRepair()
    {
        var repairRequests = new List<string>();

        var read = await ReadAsync(VALID_ANSWER, repairRequests, ANSWER_WITHOUT_JSON);

        Assert.Multiple(() =>
        {
            Assert.That(read.Result?.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(read.Issue, Is.Empty);
            Assert.That(repairRequests, Is.Empty);
        });
    }

    [Test]
    public async Task ARepairedAnswerIsTaken()
    {
        var repairRequests = new List<string>();

        var read = await ReadAsync(ANSWER_WITHOUT_EXPLANATION, repairRequests, VALID_ANSWER);

        Assert.Multiple(() =>
        {
            Assert.That(read.Result?.Findings.Single().Explanation, Is.EqualTo("The documents describe long employee tenure."));
            Assert.That(read.Issue, Is.Empty);
            Assert.That(repairRequests.Single(), Is.EqualTo("Finding 1 has no explanation."));
        });
    }

    [Test]
    public async Task TwoUnreadableAnswersFailWithTheIssueOfTheSecond()
    {
        var repairRequests = new List<string>();

        var read = await ReadAsync(ANSWER_WITHOUT_EXPLANATION, repairRequests, ANSWER_WITHOUT_JSON);

        Assert.Multiple(() =>
        {
            Assert.That(read.Result, Is.Null);
            Assert.That(read.Issue, Does.Contain("no complete JSON object"));
            Assert.That(repairRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task AFailedRepairRequestKeepsTheIssueOfTheFirstAnswer()
    {
        var repairRequests = new List<string>();

        // The provider failed, so the repair round brought no answer:
        var read = await ReadAsync(ANSWER_WITHOUT_EXPLANATION, repairRequests, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(read.Result, Is.Null);
            Assert.That(read.Issue, Is.EqualTo("Finding 1 has no explanation."));
            Assert.That(repairRequests, Has.Count.EqualTo(1));
        });
    }

    private static Task<SwotReadAnswer<SwotCategoryAnalysisResult>> ReadAsync(string answer, List<string> repairRequests, string repairedAnswer) =>
        SwotModelAnswer.ReadWithOneRepairAsync<SwotCategoryAnalysisResult>(answer, SwotCategoryAnalysisResponseParser.TryParse, issue =>
        {
            repairRequests.Add(issue);
            return Task.FromResult(repairedAnswer);
        });
}