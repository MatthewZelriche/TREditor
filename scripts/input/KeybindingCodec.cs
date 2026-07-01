#nullable enable

using System;
using System.Globalization;
using Godot;

public static class KeybindingCodec
{
    public const string Unbound = "v1:none";

    public static string Encode(InputBinding? binding) =>
        binding switch
        {
            null => Unbound,
            KeyInputBinding key => $"v1:key:{(long)key.Keycode}:{(int)key.Modifiers}",
            MouseInputBinding mouse => $"v1:mouse:{(int)mouse.Button}:{(int)mouse.Modifiers}",
            _ => throw new ArgumentOutOfRangeException(nameof(binding)),
        };

    public static bool TryDecode(string? encoded, out InputBinding? binding)
    {
        binding = null;
        if (encoded == Unbound)
            return true;
        if (string.IsNullOrWhiteSpace(encoded))
            return false;

        string[] parts = encoded.Split(':');
        if (
            parts.Length != 4
            || parts[0] != "v1"
            || !int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int mask
            )
            || !IsValidModifierMask(mask)
        )
        {
            return false;
        }

        InputBindingModifiers modifiers = (InputBindingModifiers)mask;
        switch (parts[1])
        {
            case "key"
                when long.TryParse(
                    parts[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long keyValue
                ):
            {
                return InputBinding.TryCreateKey((Key)keyValue, modifiers, out binding);
            }
            case "mouse"
                when int.TryParse(
                    parts[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int buttonValue
                ):
            {
                return InputBinding.TryCreateMouse(
                    (MouseButton)buttonValue,
                    modifiers,
                    out binding
                );
            }
            default:
                return false;
        }
    }

    private static bool IsValidModifierMask(int mask)
    {
        const InputBindingModifiers all =
            InputBindingModifiers.Shift
            | InputBindingModifiers.Ctrl
            | InputBindingModifiers.Alt
            | InputBindingModifiers.Meta;
        return mask >= 0 && ((InputBindingModifiers)mask & ~all) == 0;
    }
}
