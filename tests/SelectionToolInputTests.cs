namespace TREditor2026.Tests;

public class SelectionToolInputTests
{
    private static readonly SelectionTarget Target = SelectionTarget.ForObject(
        new EditorObjectId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
    );
    private static readonly SelectionTarget OtherTarget = SelectionTarget.ForObject(
        new EditorObjectId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
    );

    private static ViewportInputModifiers NoModifiers => new(false, false, false, false);
    private static ViewportInputModifiers ShiftOnly => new(true, false, false, false);
    private static ViewportInputModifiers CtrlOnly => new(false, true, false, false);

    [Fact]
    public void ResolveSelectionAfterPick_MissWithoutModifiers_ClearsSelection()
    {
        SelectionSnapshot before = SelectionSnapshot.From([Target]);

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: false,
            Target,
            NoModifiers
        );

        Assert.Equal(SelectionSnapshot.Empty, after);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ResolveSelectionAfterPick_MissWithModifier_IgnoresInput(bool shift, bool ctrl)
    {
        SelectionSnapshot before = SelectionSnapshot.From([Target]);

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: false,
            Target,
            new ViewportInputModifiers(shift, ctrl, false, false)
        );

        Assert.Null(after);
    }

    [Fact]
    public void ResolveSelectionAfterPick_HitWithoutModifiers_ReplacesSelection()
    {
        SelectionSnapshot before = SelectionSnapshot.From([Target]);

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: true,
            OtherTarget,
            NoModifiers
        );

        Assert.Equal(SelectionSnapshot.From([OtherTarget]), after);
    }

    [Fact]
    public void ResolveSelectionAfterPick_HitWithShift_AddsToSelection()
    {
        SelectionSnapshot before = SelectionSnapshot.From([Target]);

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: true,
            OtherTarget,
            ShiftOnly
        );

        Assert.Equal(SelectionSnapshot.From([Target, OtherTarget]), after);
    }

    [Fact]
    public void ResolveSelectionAfterPick_HitWithCtrl_TogglesTarget()
    {
        SelectionSnapshot before = SelectionSnapshot.From([Target, OtherTarget]);

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: true,
            Target,
            CtrlOnly
        );

        Assert.Equal(SelectionSnapshot.From([OtherTarget]), after);
    }

    [Fact]
    public void ResolveSelectionAfterPick_CtrlTakesPriorityOverShift()
    {
        SelectionSnapshot before = SelectionSnapshot.Empty;

        SelectionSnapshot? after = SelectionToolInput.ResolveSelectionAfterPick(
            before,
            pickSucceeded: true,
            Target,
            new ViewportInputModifiers(true, true, false, false)
        );

        Assert.Equal(SelectionSnapshot.From([Target]), after);
    }
}
