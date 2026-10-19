using System.Net;

using AIStudio.Provider;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks which answers of a transcription provider refuse the media file itself.
/// </summary>
/// <remarks>
/// A data source records such a file for good, until it changes. An answer counted here by
/// mistake keeps a file out of the index; one missed here uploads the file again with every run.
/// </remarks>
[TestFixture]
public sealed class MediaFileRejectionTests
{
    private const string OPENAI_DAMAGED_FILE = """
        {
          "error": {
            "message": "Audio file might be corrupted or unsupported",
            "type": "invalid_request_error",
            "param": "file",
            "code": "invalid_value"
          }
        }
        """;

    private const string OPENAI_UNKNOWN_MODEL = """
        {
          "error": {
            "message": "Invalid model.",
            "type": "invalid_request_error",
            "param": "model",
            "code": "invalid_value"
          }
        }
        """;

    private const string UNDECODABLE_FORMAT = """
        {
          "error": {
            "message": "Could not decode the audio file.",
            "param": "file"
          }
        }
        """;

    [Test]
    public void AFileTheProviderCannotReadIsRefused()
    {
        Assert.That(BaseProvider.IsMediaFileRejection(HttpStatusCode.BadRequest, OPENAI_DAMAGED_FILE), Is.True);
    }

    [TestCase(HttpStatusCode.RequestEntityTooLarge)]
    [TestCase(HttpStatusCode.UnsupportedMediaType)]
    public void AFileTooLargeOrOfAnUnacceptedTypeIsRefused(HttpStatusCode statusCode)
    {
        Assert.That(BaseProvider.IsMediaFileRejection(statusCode, string.Empty), Is.True);
    }

    [Test]
    public void AProviderWhichDecodesNoWebMRefusesNoSingleFile()
    {
        Assert.That(BaseProvider.IsMediaFileRejection(HttpStatusCode.BadRequest, UNDECODABLE_FORMAT), Is.False, "Every file fails the same way, and only another provider helps.");
    }

    [TestCase(HttpStatusCode.BadRequest, OPENAI_UNKNOWN_MODEL)]
    [TestCase(HttpStatusCode.BadRequest, "Bad request")]
    [TestCase(HttpStatusCode.BadRequest, "")]
    [TestCase(HttpStatusCode.TooManyRequests, OPENAI_DAMAGED_FILE)]
    [TestCase(HttpStatusCode.Unauthorized, "")]
    [TestCase(HttpStatusCode.InternalServerError, "")]
    [TestCase(HttpStatusCode.ServiceUnavailable, "")]
    public void AFailureWhichMayPassRefusesNoFile(HttpStatusCode statusCode, string responseBody)
    {
        Assert.That(BaseProvider.IsMediaFileRejection(statusCode, responseBody), Is.False);
    }
}