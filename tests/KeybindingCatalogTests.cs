using Godot;

public sealed class KeybindingCatalogTests
{
    [Fact]
    public void Definitions_HaveUniqueActionIdsAndBindings()
    {
        Assert.NotEmpty(KeybindingCatalog.All);
        Assert.Equal(
            KeybindingCatalog.All.Count,
            KeybindingCatalog.All.Select(definition => definition.ActionId).Distinct().Count()
        );

        InputBinding[] defaults = KeybindingCatalog
            .All.Select(definition => definition.DefaultBinding)
            .OfType<InputBinding>()
            .ToArray();
        Assert.Equal(defaults.Length, defaults.Distinct().Count());
        Assert.DoesNotContain(
            defaults,
            binding => binding is MouseInputBinding { Button: MouseButton.Left }
        );
    }

    [Fact]
    public void Definitions_UseTheExpectedDefaultMap()
    {
        Assert.Equal(
            new KeyInputBinding(Key.S, InputBindingModifiers.Ctrl),
            KeybindingCatalog.Get(KeybindingActions.FileSave).DefaultBinding
        );
        Assert.Equal(
            new KeyInputBinding(Key.Z, InputBindingModifiers.Ctrl | InputBindingModifiers.Shift),
            KeybindingCatalog.Get(KeybindingActions.EditRedo).DefaultBinding
        );
        Assert.Null(KeybindingCatalog.Get(KeybindingActions.ToolSelect).DefaultBinding);
        Assert.Equal(
            new MouseInputBinding(MouseButton.Right),
            KeybindingCatalog.Get(KeybindingActions.CameraLook).DefaultBinding
        );
    }

    [Fact]
    public void NormalizeKey_TreatsKeypadEnterAsEnter()
    {
        Assert.Equal(Key.Enter, InputBinding.NormalizeKey(Key.KpEnter));
        Assert.Equal(Key.A, InputBinding.NormalizeKey(Key.A));
    }

    [Fact]
    public void BindingCreation_RequiresABaseKeyAndReservesPrimaryMouse()
    {
        Assert.False(InputBinding.TryCreateKey(Key.Ctrl, InputBindingModifiers.None, out _));
        Assert.False(
            InputBinding.TryCreateMouse(MouseButton.Left, InputBindingModifiers.None, out _)
        );
        Assert.False(
            InputBinding.TryCreateMouse(MouseButton.WheelUp, InputBindingModifiers.None, out _)
        );
        Assert.True(
            InputBinding.TryCreateKey(
                Key.Z,
                InputBindingModifiers.Ctrl | InputBindingModifiers.Shift,
                out InputBinding? chord
            )
        );
        Assert.Equal(
            new KeyInputBinding(Key.Z, InputBindingModifiers.Ctrl | InputBindingModifiers.Shift),
            chord
        );
    }
}
