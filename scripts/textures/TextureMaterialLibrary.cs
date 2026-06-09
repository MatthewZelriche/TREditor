#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Session-owned mapping from stable texture asset IDs to positive mesh material slots and lazily
/// loaded repeating materials.
/// </summary>
public sealed class TextureMaterialLibrary
{
    private const int CheckerSize = 2;
    internal const BaseMaterial3D.ShadingModeEnum SurfaceShadingMode =
        BaseMaterial3D.ShadingModeEnum.Unshaded;

    private readonly Dictionary<int, string> _assetIdsBySlot = [];
    private readonly Dictionary<string, int> _slotsByAssetId = new(StringComparer.Ordinal);
    private readonly LazyResourceCache<string, Material> _materials;
    private int _nextSlot = 1;

    public TextureMaterialLibrary()
        : this(_ => null) { }

    /// <summary>
    /// Creates a library that resolves normalized asset IDs through <paramref name="loadTexture"/>.
    /// When no resolver is supplied through this overload, mapped slots use the fallback material
    /// without attempting to load from an implicit texture root.
    /// </summary>
    public TextureMaterialLibrary(Func<string, Texture2D?> loadTexture)
    {
        ArgumentNullException.ThrowIfNull(loadTexture);
        _materials = new LazyResourceCache<string, Material>(
            assetId => TryLoadTextureMaterial(loadTexture, assetId),
            CreateFallbackMaterial
        );
    }

    /// <summary>
    /// Returns the existing positive slot for <paramref name="assetId"/>, or allocates the next
    /// unused positive slot.
    /// </summary>
    public int GetOrCreateSlot(string assetId)
    {
        string normalized = NormalizeAssetId(assetId);
        if (_slotsByAssetId.TryGetValue(normalized, out int existing))
            return existing;

        while (_assetIdsBySlot.ContainsKey(_nextSlot))
            _nextSlot++;

        int slot = _nextSlot++;
        AddMapping(slot, normalized);
        return slot;
    }

    /// <summary>
    /// Restores a persistent positive slot-to-asset mapping before loaded meshes are rendered.
    /// Re-registering the same mapping is harmless; conflicting mappings are rejected.
    /// </summary>
    public void RegisterSlot(int slot, string assetId)
    {
        if (slot <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(slot),
                "Texture material slots must be positive."
            );

        string normalized = NormalizeAssetId(assetId);
        if (_assetIdsBySlot.TryGetValue(slot, out string? existingAssetId))
        {
            if (existingAssetId == normalized)
                return;
            throw new InvalidOperationException(
                $"Material slot {slot} is already mapped to '{existingAssetId}'."
            );
        }

        if (_slotsByAssetId.TryGetValue(normalized, out int existingSlot))
        {
            throw new InvalidOperationException(
                $"Texture asset '{normalized}' is already mapped to material slot {existingSlot}."
            );
        }

        AddMapping(slot, normalized);
    }

    public bool TryGetAssetId(int slot, out string? assetId) =>
        _assetIdsBySlot.TryGetValue(slot, out assetId);

    public bool TryGetSlot(string assetId, out int slot) =>
        _slotsByAssetId.TryGetValue(NormalizeAssetId(assetId), out slot);

    /// <summary>
    /// Returns mappings in stable slot order for future project serialization.
    /// </summary>
    public IReadOnlyList<MaterialSlotMapping> GetMappings() =>
        _assetIdsBySlot
            .OrderBy(pair => pair.Key)
            .Select(pair => new MaterialSlotMapping(pair.Key, pair.Value))
            .ToArray();

    /// <summary>
    /// Lazily resolves and caches the repeating material for a registered positive slot.
    /// Missing or invalid textures resolve to a shared checkerboard fallback.
    /// </summary>
    public Material ResolveMaterial(int slot)
    {
        if (!_assetIdsBySlot.TryGetValue(slot, out string? assetId))
            throw new KeyNotFoundException(
                $"No texture asset is registered for material slot {slot}."
            );
        return _materials.Resolve(assetId);
    }

    public void ClearResolvedMaterials()
    {
        _materials.Clear();
    }

    public static string NormalizeAssetId(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
            throw new ArgumentException("Texture asset ID must not be empty.", nameof(assetId));

        string normalized = assetId.Trim().Replace('\\', '/');
        if (normalized.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException(
                "Texture asset ID must be a normalized root-relative path.",
                nameof(assetId)
            );
        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        normalized = normalized.TrimStart('/');

        if (normalized.Length == 0)
            throw new ArgumentException("Texture asset ID must not be empty.", nameof(assetId));

        string[] segments = normalized.Split('/');
        if (segments.Any(segment => segment is "." or ".." || segment.Length == 0))
            throw new ArgumentException(
                "Texture asset ID must be a normalized root-relative path.",
                nameof(assetId)
            );

        return string.Join('/', segments);
    }

    private void AddMapping(int slot, string assetId)
    {
        _assetIdsBySlot.Add(slot, assetId);
        _slotsByAssetId.Add(assetId, slot);
    }

    private static Material? CreateTextureMaterial(Texture2D? texture)
    {
        if (texture == null)
            return null;

        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            ShadingMode = SurfaceShadingMode,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps,
            TextureRepeat = true,
        };
    }

    private static Material? TryLoadTextureMaterial(
        Func<string, Texture2D?> loadTexture,
        string assetId
    )
    {
        try
        {
            return CreateTextureMaterial(loadTexture(assetId));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load texture asset '{assetId}': {exception.Message}");
            return null;
        }
    }

    private static Material CreateFallbackMaterial()
    {
        var image = Image.CreateEmpty(CheckerSize, CheckerSize, false, Image.Format.Rgba8);
        image.SetPixel(0, 0, Colors.Magenta);
        image.SetPixel(1, 1, Colors.Magenta);
        image.SetPixel(1, 0, Colors.Black);
        image.SetPixel(0, 1, Colors.Black);

        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(image),
            ShadingMode = SurfaceShadingMode,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            TextureRepeat = true,
        };
    }
}
