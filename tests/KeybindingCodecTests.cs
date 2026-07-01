using Godot;

public sealed class KeybindingCodecTests
{
    public static TheoryData<InputBinding?> Bindings =>
        new()
        {
            null,
            new KeyInputBinding(Key.Enter),
            new KeyInputBinding(Key.Z, InputBindingModifiers.Ctrl | InputBindingModifiers.Shift),
            new MouseInputBinding(MouseButton.Xbutton1, InputBindingModifiers.Alt),
        };

    [Theory]
    [MemberData(nameof(Bindings))]
    public void EncodeAndDecode_RoundTrips(InputBinding? expected)
    {
        string encoded = KeybindingCodec.Encode(expected);

        bool decoded = KeybindingCodec.TryDecode(encoded, out InputBinding? actual);

        Assert.True(decoded);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v2:key:65:0")]
    [InlineData("v1:key:not-a-number:0")]
    [InlineData("v1:key:65:16")]
    [InlineData("v1:key:0:0")]
    [InlineData("v1:mouse:1:0")]
    [InlineData("v1:mouse:999:0")]
    public void TryDecode_RejectsInvalidValues(string? encoded)
    {
        Assert.False(KeybindingCodec.TryDecode(encoded, out _));
    }

    [Fact]
    public void TryDecode_NormalizesKeypadEnter()
    {
        string encoded = $"v1:key:{(long)Key.KpEnter}:0";

        Assert.True(KeybindingCodec.TryDecode(encoded, out InputBinding? binding));
        Assert.Equal(new KeyInputBinding(Key.Enter), binding);
    }
}
