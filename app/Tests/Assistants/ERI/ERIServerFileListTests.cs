using AIStudio.Assistants.ERI;

namespace AIStudio.Tests.Assistants.ERI;

/// <summary>
/// Checks how the ERI Server assistant reads which files the model wants to create.
/// </summary>
/// <remarks>
/// Up to v26.10.1, the assistant asked for invalid JSON and took every string of the answer for a file,
/// apart from the word "files". A path with a space was lost that way, and any other key the model came
/// up with became a file of its own.
/// </remarks>
[TestFixture]
public sealed class ERIServerFileListTests
{
    private static readonly string[] FILES = ["docker-compose.yml", "src/my-server/main.py", "docs/read me.md"];

    private static readonly string[] PATH_WITH_BACKSLASH = [@"src\main.py"];

    private static readonly string[] MAIN_PY = ["main.py"];

    [Test]
    public void AListBetweenProseAndAFenceIsRead()
    {
        const string ANSWER = """
                              Here are the files:

                              ```json
                              { "files": [ "docker-compose.yml", "src/my-server/main.py", "docs/read me.md" ] }
                              ```
                              """;

        var isRead = ERIServerFileList.TryParse(ANSWER, out var files);

        Assert.Multiple(() =>
        {
            Assert.That(isRead, Is.True);
            Assert.That(files, Is.EqualTo(FILES));
        });
    }

    [Test]
    public void EscapedCharactersAreDecoded()
    {
        var isRead = ERIServerFileList.TryParse("""{ "files": [ "src\\main.py" ] }""", out var files);

        Assert.Multiple(() =>
        {
            Assert.That(isRead, Is.True);
            Assert.That(files, Is.EqualTo(PATH_WITH_BACKSLASH));
        });
    }

    [Test]
    public void EmptyPathsAreLeftOut()
    {
        var isRead = ERIServerFileList.TryParse("""{ "files": [ "main.py", "", " " ] }""", out var files);

        Assert.Multiple(() =>
        {
            Assert.That(isRead, Is.True);
            Assert.That(files, Is.EqualTo(MAIN_PY));
        });
    }

    [TestCase("""[ "main.py" ]""", TestName = "A list without its object")]
    [TestCase("""{ "paths": [ "main.py" ] }""", TestName = "A key other than files")]
    [TestCase("""{ "files": [] }""", TestName = "An empty list")]
    [TestCase("""{ "files": [ "main.py", }""", TestName = "Broken JSON")]
    [TestCase("""{ "files": "main.py" }""", TestName = "A single path instead of a list")]
    [TestCase("The files are main.py and README.md.", TestName = "No JSON at all")]
    public void AnAnswerWithoutAListOfFilesIsRefused(string answer)
    {
        var isRead = ERIServerFileList.TryParse(answer, out var files);

        Assert.Multiple(() =>
        {
            Assert.That(isRead, Is.False);
            Assert.That(files, Is.Empty);
        });
    }
}