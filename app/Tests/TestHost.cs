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
    public void PrepareStaticApplicationState()
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