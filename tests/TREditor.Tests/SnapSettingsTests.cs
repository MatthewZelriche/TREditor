namespace TREditor.Tests;

public sealed class SnapSettingsTests
{
    [Fact]
    public void DefaultsMatchTheBottomBar()
    {
        var settings = new SnapSettings();

        Assert.Equal(1.0f, settings.GridSnap);
        Assert.Null(settings.AngleSnap);
    }

    [Fact]
    public void SetGridSnapRaisesChangedOnlyWhenTheValueChanges()
    {
        var settings = new SnapSettings();
        int changedCount = 0;
        settings.Changed += () => changedCount++;

        settings.SetGridSnap(1.0f);
        settings.SetGridSnap(4.0f);
        settings.SetGridSnap(null);

        Assert.Null(settings.GridSnap);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void SetAngleSnapRaisesChangedOnlyWhenTheValueChanges()
    {
        var settings = new SnapSettings();
        int changedCount = 0;
        settings.Changed += () => changedCount++;

        settings.SetAngleSnap(null);
        settings.SetAngleSnap(15.0f);
        settings.SetAngleSnap(15.0f);

        Assert.Equal(15.0f, settings.AngleSnap);
        Assert.Equal(1, changedCount);
    }
}
