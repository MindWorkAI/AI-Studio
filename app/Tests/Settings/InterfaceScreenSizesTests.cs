using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how the settings remember the interface size of each screen.
/// </summary>
/// <remarks>
/// The list keeps the most recently set screen first and forgets the oldest beyond its limit, so that a
/// user who meets many screens, e.g., at changing desks, does not grow the settings without end.
/// </remarks>
[TestFixture]
public sealed class InterfaceScreenSizesTests
{
    private const string LAPTOP = "1920x1200@287x179mm";
    private const string MONITOR = "3840x2160@597x336mm";

    [Test]
    public void AScreenTheUserChoseNothingForHasNoSize() => Assert.That(new DataApp().GetInterfaceScreenSize(LAPTOP), Is.Null);

    [Test]
    public void TheMostRecentlySetScreenComesFirst()
    {
        var app = new DataApp();
        app.RememberInterfaceScreenSize(new(LAPTOP, InterfaceSize.APP_DEFAULT, null));
        app.RememberInterfaceScreenSize(new(MONITOR, InterfaceSize.CUSTOM, 150));

        Assert.That(app.InterfaceScreenSizes.Select(screen => screen.ScreenKey), Is.EqualTo(new[] { MONITOR, LAPTOP }));
    }

    [Test]
    public void SettingAScreenAgainReplacesItsSizeAndMovesItToTheFront()
    {
        var app = new DataApp();
        app.RememberInterfaceScreenSize(new(LAPTOP, InterfaceSize.CUSTOM, 120));
        app.RememberInterfaceScreenSize(new(MONITOR, InterfaceSize.SYSTEM_SCALING, null));
        app.RememberInterfaceScreenSize(new(LAPTOP, InterfaceSize.SYSTEM_SCALING, 120));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(app.InterfaceScreenSizes.Select(screen => screen.ScreenKey), Is.EqualTo(new[] { LAPTOP, MONITOR }));
            Assert.That(app.GetInterfaceScreenSize(LAPTOP), Is.EqualTo(new InterfaceScreenSize(LAPTOP, InterfaceSize.SYSTEM_SCALING, 120)));
        }
    }

    [Test]
    public void BeyondTheLimitTheScreenSetTheLongestTimeAgoIsForgotten()
    {
        var app = new DataApp();
        for (var screen = 0; screen <= DataApp.MAX_REMEMBERED_INTERFACE_SCREENS; screen++)
            app.RememberInterfaceScreenSize(new($"{screen}x1080", InterfaceSize.CUSTOM, 100));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(app.InterfaceScreenSizes, Has.Count.EqualTo(DataApp.MAX_REMEMBERED_INTERFACE_SCREENS));
            Assert.That(app.GetInterfaceScreenSize("0x1080"), Is.Null);
            Assert.That(app.GetInterfaceScreenSize("1x1080"), Is.Not.Null);
        }
    }
}