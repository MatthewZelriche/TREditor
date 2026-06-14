using Godot;

namespace TREditor2026.Tests;

public sealed class TextureMaterialLibraryTests
{
    [Fact]
    public void GetOrCreateSlot_NormalizesAssetIdAndReturnsStableSlot()
    {
        TextureMaterialLibrary library = CreateLibrary();

        int first = library.GetOrCreateSlot(@" walls\\brick.png ");
        int second = library.GetOrCreateSlot("walls/brick.png");

        Assert.Equal(first, second);
        Assert.True(first > 0);
        Assert.True(library.TryGetAssetId(first, out string? assetId));
        Assert.Equal("walls/brick.png", assetId);
        Assert.True(library.TryGetSlot("walls\\brick.png", out int foundSlot));
        Assert.Equal(first, foundSlot);
    }

    [Fact]
    public void RegisterSlot_RestoresMappingAndAutomaticAllocationSkipsIt()
    {
        TextureMaterialLibrary library = CreateLibrary();

        library.RegisterSlot(1, "walls/brick.png");
        int slot = library.GetOrCreateSlot("floors/metal.png");

        Assert.Equal(2, slot);
        Assert.Equal(
            [
                new MaterialSlotMapping(1, "walls/brick.png"),
                new MaterialSlotMapping(2, "floors/metal.png"),
            ],
            library.GetMappings()
        );
    }

    [Fact]
    public void RegisterSlot_HighRestoredSlotDoesNotWasteLowerAvailableSlots()
    {
        TextureMaterialLibrary library = CreateLibrary();
        library.RegisterSlot(10, "walls/restored.png");

        Assert.Equal(1, library.GetOrCreateSlot("walls/new.png"));
    }

    [Fact]
    public void RegisterSlot_RejectsConflictingSlotOrAssetMappings()
    {
        TextureMaterialLibrary library = CreateLibrary();
        library.RegisterSlot(4, "walls/brick.png");

        Assert.Throws<InvalidOperationException>(() => library.RegisterSlot(4, "walls/metal.png"));
        Assert.Throws<InvalidOperationException>(() => library.RegisterSlot(5, "walls/brick.png"));
    }

    [Fact]
    public void Clear_ResetsMappingsAndSlotAllocation()
    {
        TextureMaterialLibrary library = CreateLibrary();
        library.GetOrCreateSlot("walls/brick.png");
        library.GetOrCreateSlot("floors/metal.png");

        library.Clear();

        Assert.Empty(library.GetMappings());
        Assert.False(library.TryGetSlot("walls/brick.png", out _));
        Assert.Equal(1, library.GetOrCreateSlot("ceilings/panel.png"));
    }

    [Fact]
    public void ResolveMaterial_RejectsUnknownSlotBeforeLoading()
    {
        TextureMaterialLibrary library = CreateLibrary();

        Assert.Throws<KeyNotFoundException>(() => library.ResolveMaterial(42));
    }

    [Fact]
    public void SurfaceShadingMode_IsUnshaded()
    {
        Assert.Equal(
            BaseMaterial3D.ShadingModeEnum.Unshaded,
            TextureMaterialLibrary.SurfaceShadingMode
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterSlot_RejectsNonPositiveSlots(int slot)
    {
        TextureMaterialLibrary library = CreateLibrary();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => library.RegisterSlot(slot, "walls/brick.png")
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../brick.png")]
    [InlineData("walls/../brick.png")]
    [InlineData("C:/textures/brick.png")]
    [InlineData("res://textures/brick.png")]
    public void NormalizeAssetId_RejectsInvalidAssetIds(string assetId)
    {
        Assert.Throws<ArgumentException>(() => TextureMaterialLibrary.NormalizeAssetId(assetId));
    }

    private static TextureMaterialLibrary CreateLibrary() => new(_ => null);
}
