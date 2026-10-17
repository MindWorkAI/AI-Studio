using System.Text;

using AIStudio.Tools.Rust;

namespace AIStudio.Tests.Tools;

public class ReadableTextDetectionTests
{
    [TestCase("main.tf", "resource \"example\" \"main\" { description = \"Grüße\" }")]
    [TestCase(".gitignore", "bin/\nobj/\n")]
    [TestCase("LICENSE", "Permission is hereby granted.")]
    [TestCase("Dockerfile", "FROM alpine:latest")]
    [TestCase("Dockerfile.development", "FROM alpine:latest")]
    [TestCase("Makefile", "build:\n\tdotnet build")]
    [TestCase(".env", "APP_ENV=development")]
    [TestCase("deployment notes (final) [β] #1", "Unusually named UTF-8 text file")]
    public void AcceptsUtf8PathsWithoutAKnownExtension(string fileName, string content)
    {
        AssertFile(Encoding.UTF8.GetBytes(content), fileName, true);
    }

    [Test]
    public void AcceptsLegacySingleByteText()
    {
        AssertFile([0x47, 0x72, 0xFC, 0xDF, 0x65], "greeting.data", true);
    }

    [Test]
    public void AcceptsBomMarkedUnicode()
    {
        var content = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes("A UTF-16 text file."))
            .ToArray();

        AssertFile(content, "utf16.data", true);
    }

    [Test]
    public void RejectsUtf32WhichTheRuntimeTextReaderDoesNotSupport()
    {
        var content = Encoding.UTF32.GetPreamble()
            .Concat(Encoding.UTF32.GetBytes("UTF-32 text"))
            .ToArray();

        AssertFile(content, "utf32.data", false);
    }

    [Test]
    public void RejectsBinaryContent()
    {
        AssertFile([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00], "archive.unknown", false);
        AssertFile([0x01, 0x02, 0x03, 0x04, 0x05], "controls.unknown", false);
    }

    [Test]
    public void RejectsAnEmptyFile()
    {
        AssertFile([], "empty.unknown", false);
    }

    [Test]
    public void RejectsAnUnreadablePath()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"ai-studio-missing-{Guid.NewGuid():N}.tf");
        Assert.That(ReadableTextDetection.IsReadableTextFile(missingPath), Is.False);
    }

    [Test]
    public void ReadsOnlyTheBeginningOfAFile()
    {
        var text = Enumerable.Repeat((byte)'a', ReadableTextDetection.PROBE_SIZE);
        var binary = Enumerable.Repeat((byte)0, ReadableTextDetection.PROBE_SIZE);

        AssertFile(text.Concat(binary).ToArray(), "text-then-binary.unknown", true);
        AssertFile(binary.Concat(text).ToArray(), "binary-then-text.unknown", false);
    }

    [Test]
    public void AcceptsACharacterWhichTheEndOfTheProbeCutsInHalf()
    {
        var text = Enumerable.Repeat((byte)'a', ReadableTextDetection.PROBE_SIZE - 1);
        var content = text.Concat(Encoding.UTF8.GetBytes("ü")).ToArray();

        AssertFile(content, "cut-character.unknown", true);
    }

    private static void AssertFile(byte[] content, string fileName, bool expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-readable-text-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, fileName);
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllBytes(path, content);
            Assert.That(ReadableTextDetection.IsReadableTextFile(path), Is.EqualTo(expected));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}