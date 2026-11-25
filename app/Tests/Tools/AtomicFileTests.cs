using System.Text;
using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that a file written through AtomicFile holds either its old or its new content, and that no temporary file
/// stays behind.
/// </summary>
/// <remarks>
/// Settings, chats, and the stores of several assistants rely on this. A file which a crash or an error left half
/// written would be read back the next time, so every way a write can end is checked here: replaced, refused,
/// failed, cancelled, and discarded.
/// </remarks>
[TestFixture]
public sealed class AtomicFileTests
{
    private const string OLD_CONTENT = "old content";

    private static readonly Encoding UTF8_WITHOUT_BOM = new UTF8Encoding(false);

    private string testRoot = string.Empty;

    private string TargetPath => Path.Combine(this.testRoot, "data.json");

    [SetUp]
    public void CreateTestRoot()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-atomic-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
    }

    [TearDown]
    public void DeleteTestRoot()
    {
        if (Directory.Exists(this.testRoot))
            Directory.Delete(this.testRoot, true);
    }

    [Test]
    public async Task ANewFileGetsItsContent()
    {
        await AtomicFile.WriteTextAsync(this.TargetPath, "new content", UTF8_WITHOUT_BOM, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo("new content"));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task AnExistingFileIsReplaced()
    {
        await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);

        await AtomicFile.WriteTextAsync(this.TargetPath, "new content", UTF8_WITHOUT_BOM, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo("new content"));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task WithoutOverwritingAnExistingFileStaysAsItIs()
    {
        await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);

        Assert.That(async () => await AtomicFile.WriteTextAsync(this.TargetPath, "new content", UTF8_WITHOUT_BOM, CancellationToken.None, overwrite: false), Throws.InstanceOf<IOException>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo(OLD_CONTENT));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task AFailingWriterLeavesTheFileAsItWasAndKeepsItsError()
    {
        await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);
        var error = new InvalidOperationException("The writer failed.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () => await AtomicFile.WriteAsync(this.TargetPath, async (stream, token) =>
        {
            await stream.WriteAsync("partial"u8.ToArray(), token);
            throw error;
        }, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thrown, Is.SameAs(error), "Cleaning up must not replace the error of the writer.");
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo(OLD_CONTENT));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task ACancelledWriteLeavesTheFileAsItWas()
    {
        await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);
        var cancelled = new CancellationToken(canceled: true);

        Assert.That(async () => await AtomicFile.WriteTextAsync(this.TargetPath, "new content", UTF8_WITHOUT_BOM, cancelled), Throws.InstanceOf<OperationCanceledException>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo(OLD_CONTENT));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ADiscardedWriteLeavesTheFileAsItWas(bool targetExists)
    {
        if (targetExists)
            await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);

        var replaced = await AtomicFile.WriteOrDiscardAsync(this.TargetPath, async (stream, token) =>
        {
            await AtomicFile.WriteTextAsync(stream, "new content", UTF8_WITHOUT_BOM, token);
            return false;
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replaced, Is.False);
            Assert.That(File.Exists(this.TargetPath), Is.EqualTo(targetExists));
            if (targetExists)
                Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo(OLD_CONTENT));

            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task FlushingToTheDiskWritesTheSameContent()
    {
        await File.WriteAllTextAsync(this.TargetPath, OLD_CONTENT);

        await AtomicFile.WriteTextAsync(this.TargetPath, "new content", UTF8_WITHOUT_BOM, CancellationToken.None, flushToDisk: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo("new content"));
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    /// <summary>
    /// The places which switched to AtomicFile keep the bytes they wrote before, the byte order mark included:
    /// chats start with one, the settings do not.
    /// </summary>
    [TestCase("Encoding.UTF8", "Grüße aus Köln 🙂\nZweite Zeile")]
    [TestCase("Encoding.UTF8", "")]
    [TestCase("UTF8Encoding(false)", "Grüße aus Köln 🙂\nZweite Zeile")]
    [TestCase("UTF8Encoding(false)", "")]
    [TestCase("UTF8Encoding(true)", "Grüße aus Köln 🙂\nZweite Zeile")]
    public async Task TextGetsTheSameBytesAsWithWriteAllText(string encodingName, string content)
    {
        var encoding = encodingName switch
        {
            "Encoding.UTF8" => Encoding.UTF8,
            "UTF8Encoding(false)" => new UTF8Encoding(false),
            _ => new UTF8Encoding(true)
        };

        var expectedPath = Path.Combine(this.testRoot, "expected.txt");
        await File.WriteAllTextAsync(expectedPath, content, encoding);

        await AtomicFile.WriteTextAsync(this.TargetPath, content, encoding, CancellationToken.None);

        Assert.That(await File.ReadAllBytesAsync(this.TargetPath), Is.EqualTo(await File.ReadAllBytesAsync(expectedPath)));
    }

    [Test]
    public async Task JsonGetsTheSameBytesAsWithSerializeAndWriteAllText()
    {
        var settings = new Data();
        var expectedPath = Path.Combine(this.testRoot, "expected.json");
        await File.WriteAllTextAsync(expectedPath, JsonSerializer.Serialize(settings, SettingsManager.JSON_OPTIONS), UTF8_WITHOUT_BOM);

        await AtomicFile.WriteJsonAsync(this.TargetPath, settings, SettingsManager.JSON_OPTIONS, CancellationToken.None);

        Assert.That(await File.ReadAllBytesAsync(this.TargetPath), Is.EqualTo(await File.ReadAllBytesAsync(expectedPath)));
    }

    /// <summary>
    /// The transcript stores recognize an interrupted write by this name: DataSourceTranscriptStore removes it, and
    /// MediaSidecarTranscripts keeps it out of the Batch Processing assistant.
    /// </summary>
    [Test]
    public async Task TheTemporaryFileLiesNextToItsTargetUnderTheNameTheTranscriptStoresKnow()
    {
        var temporaryFiles = new List<string>();
        await AtomicFile.WriteAsync(this.TargetPath, (_, _) =>
        {
            temporaryFiles.AddRange(this.Leftovers().Select(path => Path.GetFileName(path)));
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.That(temporaryFiles, Has.Count.EqualTo(1));
        Assert.That(temporaryFiles[0], Does.Match(@"^data\.json\.[0-9a-f]{32}\.tmp$"));
    }

    /// <summary>
    /// Two writes of the same file overlap, e.g., when two runs of the Batch Processing assistant write into the same
    /// folder. Each one needs a temporary file of its own; with a shared one, the second write would fail.
    /// </summary>
    [Test]
    public async Task OverlappingWritesOfTheSameFileDoNotCollide()
    {
        await AtomicFile.WriteAsync(this.TargetPath, async (stream, token) =>
        {
            await AtomicFile.WriteTextAsync(stream, "first", UTF8_WITHOUT_BOM, token);
            await AtomicFile.WriteTextAsync(this.TargetPath, "second", UTF8_WITHOUT_BOM, token);
        }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await File.ReadAllTextAsync(this.TargetPath), Is.EqualTo("first"), "The write which finished last wins.");
            Assert.That(this.Leftovers(), Is.Empty);
        }
    }

    [Test]
    public async Task DeletingTemporaryFilesRemovesTheLeftoversOfBothFormsAndNothingElse()
    {
        var nested = Directory.CreateDirectory(Path.Combine(this.testRoot, "nested", "deeper")).FullName;
        var current = Path.Combine(nested, $"project.json.{Guid.NewGuid():N}.tmp");
        var previous = Path.Combine(this.testRoot, $"project.json.tmp-{Guid.NewGuid():N}");
        string[] unrelated =
        [
            Path.Combine(this.testRoot, "project.json"),
            Path.Combine(this.testRoot, "notes.tmp"),
            Path.Combine(nested, "draft.tmp-copy"),
            Path.Combine(nested, $"archive.{Guid.NewGuid():N}.tmp.bak"),
            Path.Combine(this.testRoot, $"report.tmp-{Guid.NewGuid():N}.txt")
        ];

        foreach (var path in unrelated.Append(current).Append(previous))
            await File.WriteAllTextAsync(path, "{");

        AtomicFile.DeleteTemporaryFiles(this.testRoot);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(current), Is.False, "A leftover of AtomicFile stayed.");
            Assert.That(File.Exists(previous), Is.False, "A leftover of an older version stayed.");
            Assert.That(unrelated.Where(path => !File.Exists(path)), Is.Empty, "A file which is no leftover was deleted.");
        }
    }

    /// <summary>
    /// Gets every file below the test folder except the target.
    /// </summary>
    private IEnumerable<string> Leftovers() => Directory.EnumerateFiles(this.testRoot, "*", SearchOption.AllDirectories).Where(path => path != this.TargetPath);
}