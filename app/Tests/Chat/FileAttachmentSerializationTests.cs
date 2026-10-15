using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that storing a file attachment never asks the file system about it.
/// </summary>
/// <remarks>
/// Whether the file still exists is a question to the file system, and on a network share which is
/// out of reach it takes until the SMB timeout to get an answer -- about 20 seconds. Chat templates
/// carry attachments, so every store of the settings would wait that long, and the plugins starting
/// during that time ran out of their budget. The answer is not worth storing anyway: it is stale the
/// moment it is written.
/// </remarks>
[TestFixture]
public sealed class FileAttachmentSerializationTests
{
    private const string FILE_PATH = @"\\10.255.255.1\share\report.pdf";

    [Test]
    public void AChatTemplateIsStoredWithoutCheckingItsAttachments()
    {
        var template = new ChatTemplate
        {
            FileAttachments = [new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", FILE_PATH, 1024)],
            ExampleConversation =
            [
                new ContentBlock
                {
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.USER,
                    Content = new ContentText
                    {
                        Text = "Summarize the report.",
                        FileAttachments = [new FileAttachmentImage("chart.png", @"\\10.255.255.1\share\chart.png", 2048)],
                    },
                },
            ],
        };

        var json = JsonSerializer.Serialize(template, SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain(FILE_PATH.Replace(@"\", @"\\")), "The attachment has to reach the JSON, or this test checks nothing.");
            Assert.That(json, Does.Contain("chart.png"), "The attachment of the example conversation has to reach the JSON, or this test checks nothing.");
            Assert.That(json, Does.Not.Contain("\"Exists\""));
        });
    }

    [Test]
    public void AnAttachmentStoredWithTheFormerFieldStillLoads()
    {
        const string LEGACY_JSON = """
                                   {
                                     "$type": "file",
                                     "Type": "DOCUMENT",
                                     "FileName": "report.pdf",
                                     "FilePath": "/tmp/report.pdf",
                                     "FileSizeBytes": 1024,
                                     "Exists": true
                                   }
                                   """;

        var read = JsonSerializer.Deserialize<FileAttachment>(LEGACY_JSON, SettingsManager.JSON_OPTIONS)!;

        Assert.Multiple(() =>
        {
            Assert.That(read.FileName, Is.EqualTo("report.pdf"));
            Assert.That(read.FilePath, Is.EqualTo("/tmp/report.pdf"));
            Assert.That(read.FileSizeBytes, Is.EqualTo(1024));
        });
    }
}