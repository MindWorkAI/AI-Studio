using AIStudio.Assistants.ERI;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.ERI;

/// <summary>
/// Checks what the ERI Server assistant reports for each file it is asked to write below the base directory.
/// </summary>
/// <remarks>
/// Up to v26.10.1, a file which was not written only showed up in the log, and an error while writing
/// ended the whole run. Now every file gets an outcome, which the assistant tells the user about.
/// </remarks>
[TestFixture]
public sealed class ERIServerFileWritingTests
{
    private const string MAIN_PY = "print('ERI server')\n";

    private const string ANSWER = $"## main.py\n\n```python\n{MAIN_PY}```";

    private string baseDirectory = string.Empty;

    [SetUp]
    public void CreateBaseDirectory()
    {
        this.baseDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-eri-server-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.baseDirectory);
    }

    [TearDown]
    public void RemoveBaseDirectory()
    {
        try
        {
            Directory.Delete(this.baseDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task AFileIsWrittenBelowTheBaseDirectory()
    {
        var outcome = await ERIServerFiles.WriteAsync(this.baseDirectory, "src/my-server/main.py", ANSWER, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ERIServerFileOutcome.WRITTEN));
            Assert.That(File.ReadAllText(Path.Join(this.baseDirectory, "src", "my-server", "main.py")), Is.EqualTo(MAIN_PY));
        });
    }

    [Test]
    public async Task APathLeadingOutsideTheBaseDirectoryIsNotWritten()
    {
        var file = $"../outside-{Guid.NewGuid():N}.py";
        var outcome = await ERIServerFiles.WriteAsync(this.baseDirectory, file, ANSWER, NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ERIServerFileOutcome.OUTSIDE_BASE_DIRECTORY));
            Assert.That(File.Exists(Path.GetFullPath(Path.Join(this.baseDirectory, file))), Is.False);
        });
    }

    [Test]
    public async Task AnAnswerWhichBreaksOffWritesNothing()
    {
        var outcome = await ERIServerFiles.WriteAsync(this.baseDirectory, "main.py", "## main.py\n\n```python\nprint(", NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ERIServerFileOutcome.NO_COMPLETE_CODE_BLOCK));
            Assert.That(File.Exists(Path.Join(this.baseDirectory, "main.py")), Is.False);
        });
    }

    [Test]
    public async Task AFileWhichCannotBeWrittenIsReportedInsteadOfEndingTheRun()
    {
        // A directory of the same name stands where the file would go:
        Directory.CreateDirectory(Path.Join(this.baseDirectory, "main.py"));

        var outcome = await ERIServerFiles.WriteAsync(this.baseDirectory, "main.py", ANSWER, NullLogger.Instance);

        Assert.That(outcome, Is.EqualTo(ERIServerFileOutcome.WRITE_FAILED));
    }
}