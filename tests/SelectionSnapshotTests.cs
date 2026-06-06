namespace TREditor2026.Tests;

public class SelectionSnapshotTests
{
    private static SelectionTarget TargetA =>
        SelectionTarget.ForObject(
            new EditorObjectId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
        );
    private static SelectionTarget TargetB =>
        SelectionTarget.ForObject(
            new EditorObjectId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
        );
    private static SelectionTarget TargetC =>
        SelectionTarget.ForVertex(
            new EditorObjectId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            new VertexHandle(1, 0)
        );

    [Fact]
    public void Empty_HasZeroCount()
    {
        Assert.True(SelectionSnapshot.Empty.IsEmpty);
        Assert.Equal(0, SelectionSnapshot.Empty.Count);
        Assert.Empty(SelectionSnapshot.Empty.Targets);
    }

    [Fact]
    public void From_DeduplicatesTargets()
    {
        SelectionSnapshot snapshot = SelectionSnapshot.From([TargetA, TargetA, TargetB]);

        Assert.Equal(2, snapshot.Count);
        Assert.Contains(TargetA, snapshot.Targets);
        Assert.Contains(TargetB, snapshot.Targets);
    }

    [Fact]
    public void From_EmptyEnumerable_ReturnsEmpty()
    {
        Assert.Equal(SelectionSnapshot.Empty, SelectionSnapshot.From([]));
    }

    [Fact]
    public void From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SelectionSnapshot.From(null!));
    }

    [Fact]
    public void Add_AddsNewTarget()
    {
        SelectionSnapshot before = SelectionSnapshot.Empty;
        SelectionSnapshot after = before.Add(TargetA);

        Assert.Equal(1, after.Count);
        Assert.Contains(TargetA, after.Targets);
    }

    [Fact]
    public void Add_ExistingTarget_ReturnsSameInstance()
    {
        SelectionSnapshot before = SelectionSnapshot.From([TargetA]);
        SelectionSnapshot after = before.Add(TargetA);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Remove_RemovesTarget()
    {
        SelectionSnapshot before = SelectionSnapshot.From([TargetA, TargetB]);
        SelectionSnapshot after = before.Remove(TargetA);

        Assert.Equal(1, after.Count);
        Assert.DoesNotContain(TargetA, after.Targets);
        Assert.Contains(TargetB, after.Targets);
    }

    [Fact]
    public void Remove_MissingTarget_ReturnsSameInstance()
    {
        SelectionSnapshot before = SelectionSnapshot.From([TargetA]);
        SelectionSnapshot after = before.Remove(TargetB);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Toggle_AddsWhenAbsent()
    {
        SelectionSnapshot after = SelectionSnapshot.Empty.Toggle(TargetA);

        Assert.Contains(TargetA, after.Targets);
    }

    [Fact]
    public void Toggle_RemovesWhenPresent()
    {
        SelectionSnapshot before = SelectionSnapshot.From([TargetA]);
        SelectionSnapshot after = before.Toggle(TargetA);

        Assert.True(after.IsEmpty);
    }

    [Theory]
    [InlineData(SelectionChangeMode.Replace)]
    [InlineData(SelectionChangeMode.Add)]
    [InlineData(SelectionChangeMode.Remove)]
    [InlineData(SelectionChangeMode.Toggle)]
    public void Apply_DelegatesToCorrectOperation(SelectionChangeMode mode)
    {
        SelectionSnapshot before = SelectionSnapshot.From([TargetA, TargetB]);
        SelectionTarget operand = mode is SelectionChangeMode.Remove or SelectionChangeMode.Toggle
            ? TargetA
            : TargetC;
        SelectionSnapshot expected = mode switch
        {
            SelectionChangeMode.Replace => before.Replace(TargetC),
            SelectionChangeMode.Add => before.Add(TargetC),
            SelectionChangeMode.Remove => before.Remove(TargetA),
            SelectionChangeMode.Toggle => before.Toggle(TargetA),
            _ => throw new InvalidOperationException(),
        };

        Assert.Equal(expected, before.Apply(mode, operand));
    }

    [Fact]
    public void Apply_InvalidMode_Throws()
    {
        SelectionSnapshot snapshot = SelectionSnapshot.Empty;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            snapshot.Apply((SelectionChangeMode)999, TargetA)
        );
    }

    [Fact]
    public void Replace_ReplacesEntireSelection()
    {
        SelectionSnapshot after = SelectionSnapshot.From([TargetA, TargetB]).Replace(TargetC);

        Assert.Equal(1, after.Count);
        Assert.Contains(TargetC, after.Targets);
    }

    [Fact]
    public void Equals_SameTargets_AreEqual()
    {
        SelectionSnapshot left = SelectionSnapshot.From([TargetA, TargetB]);
        SelectionSnapshot right = SelectionSnapshot.From([TargetA, TargetB]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentOrder_AreNotEqual()
    {
        SelectionSnapshot left = SelectionSnapshot.From([TargetA, TargetB]);
        SelectionSnapshot right = SelectionSnapshot.From([TargetB, TargetA]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Contains_ReflectsTargets()
    {
        SelectionSnapshot snapshot = SelectionSnapshot.From([TargetA]);

        Assert.True(snapshot.Contains(TargetA));
        Assert.False(snapshot.Contains(TargetB));
    }
}
