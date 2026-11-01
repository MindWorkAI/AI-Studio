using System.Reflection;

using AIStudio.Tools.Metadata;

namespace AIStudio.Tests.Tools.Metadata;

/// <summary>
/// Checks the versions of .NET which the information page shows.
/// </summary>
/// <remarks>
/// The page names the runtime which runs the app and the SDK which built it, each with the commit it
/// was built from. Both may differ from what metadata.txt says: a release brings its own runtime, a
/// development build runs on whatever is installed, and a local build may use another SDK than the
/// pipeline.
/// </remarks>
[TestFixture]
public sealed class DotnetVersionTests
{
    private const string VERSION_WITH_COMMIT = @"^\d+\.\d+\.\d+\S* \(commit [0-9a-f]{10}\)$";

    [TestCase("10.0.14+0427b9abbead3fa64c6655eb4290c537397131fa", ExpectedResult = "10.0.14 (commit 0427b9abbe)", TestName = "A runtime with its commit")]
    [TestCase("10.0.0-rc.2.25502.107+89c8f6a112d37d2ea8b77821e56d170a1bccdc5a", ExpectedResult = "10.0.14 (commit 89c8f6a112)", TestName = "A runtime whose informational version has a label")]
    [TestCase("10.0.14", ExpectedResult = "10.0.14", TestName = "A runtime without a commit")]
    [TestCase("10.0.14+0427b9", ExpectedResult = "10.0.14", TestName = "A runtime with a cut-off commit")]
    [TestCase("", ExpectedResult = "10.0.14", TestName = "A runtime without an informational version")]
    [TestCase(null, ExpectedResult = "10.0.14", TestName = "A runtime whose core library names no version")]
    public string TheRuntimeIsDescribedWithItsCommit(string? informationalVersion) => DotnetRuntimeVersion.Describe(new Version(10, 0, 14), informationalVersion);

    [Test]
    public void TheRuntimeIsTheOneRunningTheApp()
    {
        Assert.That(DotnetRuntimeVersion.Current, Does.StartWith($"{Environment.Version} ").And.Match(VERSION_WITH_COMMIT));
    }

    [Test]
    public void TheSdkIsNamedWithItsCommit()
    {
        var metaData = typeof(DotnetRuntimeVersion).Assembly.GetCustomAttribute<MetaDataAttribute>();
        Assert.That(metaData?.DotnetSdkVersion, Does.Match(VERSION_WITH_COMMIT));
    }
}