using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what kind of content a passage of a local file is said to be.
/// </summary>
/// <remarks>
/// The AI is told the kind with every passage, and the user sees it as well: a passage of an audio
/// or a video file is the transcript of the recording, which is what was indexed and searched, and
/// the viewer marks it as one. Called anything else, the words of a meeting would pass for a document.
/// </remarks>
[TestFixture]
public sealed class LocalRetrievalContentTypeTests
{
    [TestCase("mp3", ExpectedResult = RetrievalContentType.TEXT_TRANSCRIPT)]
    [TestCase("M4A", ExpectedResult = RetrievalContentType.TEXT_TRANSCRIPT)]
    [TestCase("mp4", ExpectedResult = RetrievalContentType.TEXT_TRANSCRIPT)]
    [TestCase("pdf", ExpectedResult = RetrievalContentType.TEXT_DOCUMENT)]
    [TestCase("xlsx", ExpectedResult = RetrievalContentType.TEXT_SPREADSHEET)]
    [TestCase("pptx", ExpectedResult = RetrievalContentType.TEXT_PRESENTATION)]
    [TestCase("unknown", ExpectedResult = RetrievalContentType.TEXT_DOCUMENT)]
    public RetrievalContentType AFileTypeTellsWhatItsPassagesAre(string fileType) => DataSourceLocalRetrievalService.GetRetrievalContentType(fileType);
}