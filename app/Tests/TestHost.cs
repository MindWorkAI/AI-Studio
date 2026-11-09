using Microsoft.Extensions.Logging.Abstractions;

//
// Deliberately without a namespace: NUnit then applies this fixture to the whole assembly, so every
// test -- the ones written today and the ones written later in some other folder -- starts with the
// static state below already in place. A second setup fixture would only ever be needed for state
// that must not leak between areas.
//
namespace AIStudio.Tests;

[SetUpFixture]
public sealed class TestHost
{
    [OneTimeSetUp]
    public void PrepareStaticApplicationState() => PrepareStaticState();

    /// <summary>
    /// Fills the static state of the app which the tests need.
    /// </summary>
    /// <remarks>
    /// NUnit builds the cases of a TestCaseSource while it discovers the tests, before this fixture runs.
    /// A source which touches a type of the app with a static logger calls this first.
    /// </remarks>
    public static void PrepareStaticState()
    {
        //
        // A number of types in the app hold a static logger field that is initialized from
        // Program.LOGGER_FACTORY, among them Settings.Provider. The app assigns that factory while
        // Kestrel comes up; in a test process nobody does, so it stays null and the first touch of
        // such a type dies inside its type initializer -- before a single assertion runs. A factory
        // that writes nowhere is all it takes to get past that.
        //
        Program.LOGGER_FACTORY = NullLoggerFactory.Instance;
    }
}