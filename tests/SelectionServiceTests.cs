namespace TREditor2026.Tests;

public sealed class SelectionServiceTests
{
    private static readonly SelectionTarget Target = SelectionTarget.ForObject(
        new EditorObjectId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
    );

    [Fact]
    public void Apply_FiresEventWithSnapshotOnlyOnChange()
    {
        SelectionService service = new();
        List<SelectionSnapshot> events = [];
        service.SelectionChanged += events.Add;

        SelectionSnapshot first = SelectionSnapshot.From([Target]);
        Assert.True(service.Apply(first));
        Assert.Equal(first, service.Current);
        Assert.Single(events);
        Assert.Equal(first, events[0]);

        Assert.False(service.Apply(first));
        Assert.Single(events);
    }

    [Fact]
    public void Apply_EmptySnapshot_FiresEvent()
    {
        SelectionService service = new();
        int eventCount = 0;
        service.SelectionChanged += _ => eventCount++;

        service.Apply(SelectionSnapshot.From([Target]));
        service.Apply(SelectionSnapshot.Empty);

        Assert.Equal(2, eventCount);
        Assert.True(service.Current.IsEmpty);
    }
}
