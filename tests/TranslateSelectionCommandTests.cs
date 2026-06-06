using Godot;

namespace TREditor2026.Tests;

public class TranslateSelectionCommandTests
{
    private static readonly SelectionTarget Target = SelectionTarget.ForObject(
        new EditorObjectId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
    );

    [Fact]
    public void CreateIfChanged_EmptySelection_ReturnsNull()
    {
        Vector3 delta = new(1, 0, 0);

        Assert.Null(TranslateSelectionCommand.CreateIfChanged(SelectionSnapshot.Empty, delta));
    }

    [Fact]
    public void CreateIfChanged_ZeroDelta_ReturnsNull()
    {
        SelectionSnapshot selection = SelectionSnapshot.From([Target]);

        Assert.Null(TranslateSelectionCommand.CreateIfChanged(selection, Vector3.Zero));
    }

    // Non-null paths construct EditorCommand (GodotObject) and require the Godot runtime.
}
