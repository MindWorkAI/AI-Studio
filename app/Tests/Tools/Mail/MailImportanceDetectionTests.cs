using AIStudio.Tools.Mail;

using MimeKit;

namespace AIStudio.Tests.Tools.Mail;

/// <summary>
/// Checks how the three headers for the importance of a mail are read.
/// </summary>
[TestFixture]
public sealed class MailImportanceDetectionTests
{
    [TestCase("Importance", "High", ExpectedResult = MailImportance.HIGH)]
    [TestCase("Importance", "low", ExpectedResult = MailImportance.LOW)]
    [TestCase("Importance", "normal", ExpectedResult = MailImportance.NORMAL)]
    [TestCase("X-Priority", "1 (Highest)", ExpectedResult = MailImportance.HIGH)]
    [TestCase("X-Priority", "2", ExpectedResult = MailImportance.HIGH)]
    [TestCase("X-Priority", "3 (Normal)", ExpectedResult = MailImportance.NORMAL)]
    [TestCase("X-Priority", "4 (Low)", ExpectedResult = MailImportance.LOW)]
    [TestCase("X-Priority", "5", ExpectedResult = MailImportance.LOW)]
    [TestCase("X-Priority", "10", ExpectedResult = MailImportance.NORMAL)]
    [TestCase("Priority", "urgent", ExpectedResult = MailImportance.HIGH)]
    [TestCase("Priority", "Non-Urgent", ExpectedResult = MailImportance.LOW)]
    public MailImportance EachHeaderIsRead(string field, string value)
    {
        var headers = new HeaderList();
        headers.Add(field, value);
        return MailImportanceDetection.Detect(headers);
    }

    [Test]
    public void TheImportanceHeaderComesFirst()
    {
        var headers = new HeaderList();
        headers.Add("X-Priority", "1 (Highest)");
        headers.Add("Importance", "low");
        Assert.That(MailImportanceDetection.Detect(headers), Is.EqualTo(MailImportance.LOW));
    }

    [Test]
    public void AValueNobodyDefinedLeavesTheDecisionToTheNextHeader()
    {
        var headers = new HeaderList();
        headers.Add("Importance", "whenever");
        headers.Add("X-Priority", "1 (Highest)");
        Assert.That(MailImportanceDetection.Detect(headers), Is.EqualTo(MailImportance.HIGH));
    }

    [Test]
    public void AMailWithoutAnyOfTheHeadersIsNormal() => Assert.That(MailImportanceDetection.Detect(new HeaderList()), Is.EqualTo(MailImportance.NORMAL));
}