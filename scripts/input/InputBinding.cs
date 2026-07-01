#nullable enable

using System;
using System.Collections.Generic;
using Godot;

[Flags]
public enum InputBindingModifiers
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
    Meta = 1 << 3,
}

public abstract record InputBinding(InputBindingModifiers Modifiers)
{
    public InputEvent ToInputEvent()
    {
        InputEventWithModifiers input = this switch
        {
            KeyInputBinding key => new InputEventKey { Keycode = key.Keycode },
            MouseInputBinding mouse => new InputEventMouseButton { ButtonIndex = mouse.Button },
            _ => throw new InvalidOperationException($"Unsupported binding type '{GetType()}'."),
        };

        input.ShiftPressed = Modifiers.HasFlag(InputBindingModifiers.Shift);
        input.CtrlPressed = Modifiers.HasFlag(InputBindingModifiers.Ctrl);
        input.AltPressed = Modifiers.HasFlag(InputBindingModifiers.Alt);
        input.MetaPressed = Modifiers.HasFlag(InputBindingModifiers.Meta);
        return input;
    }

    public IEnumerable<InputEvent> ToInputEvents()
    {
        yield return ToInputEvent();
        if (this is KeyInputBinding { Keycode: Key.Enter } enter)
            yield return new KeyInputBinding(Key.KpEnter, enter.Modifiers).ToInputEvent();
    }

    public string GetDisplayText()
    {
        List<string> parts = [];
        if (Modifiers.HasFlag(InputBindingModifiers.Ctrl))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(InputBindingModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(InputBindingModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(InputBindingModifiers.Meta))
            parts.Add("Meta");

        parts.Add(
            this switch
            {
                KeyInputBinding key => GetKeyDisplayText(key.Keycode),
                MouseInputBinding mouse => GetMouseDisplayText(mouse.Button),
                _ => throw new InvalidOperationException(
                    $"Unsupported binding type '{GetType()}'."
                ),
            }
        );
        return string.Join("+", parts);
    }

    public static bool TryCapture(InputEvent input, out InputBinding? binding)
    {
        binding = null;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            return TryCreateKey(key.Keycode, GetModifiers(key), out binding);
        }

        if (
            input is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: not MouseButton.None and not MouseButton.Left
            } mouse
        )
        {
            return TryCreateMouse(mouse.ButtonIndex, GetModifiers(mouse), out binding);
        }

        return false;
    }

    public static bool TryCreateKey(
        Key key,
        InputBindingModifiers modifiers,
        out InputBinding? binding
    )
    {
        key = NormalizeKey(key);
        if (key == Key.None || IsModifierKey(key))
        {
            binding = null;
            return false;
        }

        binding = new KeyInputBinding(key, modifiers);
        return true;
    }

    public static bool TryCreateMouse(
        MouseButton button,
        InputBindingModifiers modifiers,
        out InputBinding? binding
    )
    {
        if (
            button
            is not (
                MouseButton.Right
                or MouseButton.Middle
                or MouseButton.Xbutton1
                or MouseButton.Xbutton2
            )
        )
        {
            binding = null;
            return false;
        }

        binding = new MouseInputBinding(button, modifiers);
        return true;
    }

    public static Key NormalizeKey(Key key) => key == Key.KpEnter ? Key.Enter : key;

    public static InputBinding? Normalize(InputBinding? binding) =>
        binding is KeyInputBinding key
            ? new KeyInputBinding(NormalizeKey(key.Keycode), key.Modifiers)
            : binding;

    private static InputBindingModifiers GetModifiers(InputEventWithModifiers input)
    {
        InputBindingModifiers modifiers = InputBindingModifiers.None;
        if (input.ShiftPressed)
            modifiers |= InputBindingModifiers.Shift;
        if (input.CtrlPressed)
            modifiers |= InputBindingModifiers.Ctrl;
        if (input.AltPressed)
            modifiers |= InputBindingModifiers.Alt;
        if (input.MetaPressed)
            modifiers |= InputBindingModifiers.Meta;
        return modifiers;
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.Shift or Key.Ctrl or Key.Alt or Key.Meta;

    private static string GetKeyDisplayText(Key key) =>
        key switch
        {
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Delete => "Delete",
            Key.Space => "Space",
            _ => key.ToString(),
        };

    private static string GetMouseDisplayText(MouseButton button) =>
        button switch
        {
            MouseButton.Right => "Right Mouse",
            MouseButton.Middle => "Middle Mouse",
            MouseButton.Xbutton1 => "Mouse 4",
            MouseButton.Xbutton2 => "Mouse 5",
            _ => $"Mouse {button}",
        };
}

public sealed record KeyInputBinding(Key Keycode, InputBindingModifiers Modifiers = 0)
    : InputBinding(Modifiers);

public sealed record MouseInputBinding(MouseButton Button, InputBindingModifiers Modifiers = 0)
    : InputBinding(Modifiers);
