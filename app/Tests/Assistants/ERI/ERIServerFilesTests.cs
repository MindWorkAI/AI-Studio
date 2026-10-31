using AIStudio.Assistants.ERI;

namespace AIStudio.Tests.Assistants.ERI;

/// <summary>
/// Checks how the ERI Server assistant reads the files of a server out of the answers of the model.
/// </summary>
/// <remarks>
/// Up to v26.10.1, a file such as docker-compose.yml never reached the disk: the regular expression which
/// read its content expected a heading without a hyphen, and the file only showed up in the log. The same
/// expression read up to the last fence of an answer, so a usage example after the file ended up in it.
/// </remarks>
[TestFixture]
public sealed class ERIServerFilesTests
{
    private const string COMPOSE_FILE = "services:\n  eri:\n    build: .\n    ports:\n      - \"8080:8080\"\n";

    private static readonly string[] PATHS_WITH_HYPHENS = ["docker-compose.yml", "src/my-server/main.py"];

    [TestCase("## docker-compose.yml", TestName = "A heading with a hyphen")]
    [TestCase("## `docker-compose.yml`", TestName = "A heading with the path in backticks")]
    [TestCase("### File: deploy/my-server/docker-compose.yml", TestName = "A heading with a label in front of the path")]
    [TestCase("Here is the file:", TestName = "No heading at all")]
    public void TheContentIsReadWhateverStandsAboveIt(string introduction)
    {
        var answer = Answer(introduction, string.Empty, "```yaml", COMPOSE_FILE.TrimEnd('\n'), "```");

        Assert.That(ERIServerFiles.ReadFileContent(answer), Is.EqualTo(COMPOSE_FILE));
    }

    [Test]
    public void ACodeBlockAfterTheFileStaysOutOfIt()
    {
        var answer = Answer(
            "## main.py",
            string.Empty,
            "```python",
            "print('ERI server')",
            "```",
            string.Empty,
            "Start the server with:",
            string.Empty,
            "```bash",
            "python main.py",
            "```");

        Assert.That(ERIServerFiles.ReadFileContent(answer), Is.EqualTo("print('ERI server')\n"));
    }

    [Test]
    public void AFileWithCodeBlocksOfItsOwnStaysWholeInALongerFence()
    {
        var answer = Answer(
            "## README.md",
            string.Empty,
            "````markdown",
            "# ERI Server",
            string.Empty,
            "```bash",
            "python main.py",
            "```",
            "````");

        Assert.That(ERIServerFiles.ReadFileContent(answer), Is.EqualTo("# ERI Server\n\n```bash\npython main.py\n```\n"));
    }

    [Test]
    public void AFenceOfTildesIsReadAsWell()
    {
        var answer = Answer("## main.py", "~~~python", "print('ERI server')", "~~~");

        Assert.That(ERIServerFiles.ReadFileContent(answer), Is.EqualTo("print('ERI server')\n"));
    }

    [Test]
    public void ACodeBlockWhichBreaksOffIsNoFile()
    {
        var answer = Answer("## main.py", "```python", "print('ERI server')");

        Assert.That(ERIServerFiles.ReadFileContent(answer), Is.Empty);
    }

    [Test]
    public void AnAnswerWithoutACodeBlockIsNoFile()
    {
        Assert.That(ERIServerFiles.ReadFileContent("I cannot create this file."), Is.Empty);
    }

    [Test]
    public void TheListOfFilesKeepsPathsWithHyphens()
    {
        var files = ERIServerFiles.ReadFileList("""{ "files": [ "docker-compose.yml", "src/my-server/main.py" ] }""");

        Assert.That(files, Is.EqualTo(PATHS_WITH_HYPHENS));
    }

    private static string Answer(params string[] lines) => string.Join('\n', lines);
}