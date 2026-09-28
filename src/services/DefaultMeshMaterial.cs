using System;
using System.Collections.Generic;
using Godot;

// Shared unlit matcap used as the default surface look for committed scene meshes.
internal sealed class DefaultMeshMaterial : IDisposable
{
    public const string ShaderPath = "res://src/shaders/matcap_unlit.gdshader";
    public const string MatcapPath = "res://assets/matcaps/matcap-cool-bluegrey.png";

    private readonly Shader _sourceShader;
    private readonly Texture2D _matcap;
    private readonly Dictionary<int, StencilMaterial> _materials = [];
    private bool _disposed;

    public DefaultMeshMaterial()
    {
        _sourceShader = GD.Load<Shader>(ShaderPath)
            ?? throw new InvalidOperationException($"Failed to load matcap shader at '{ShaderPath}'.");
        _matcap = GD.Load<Texture2D>(MatcapPath)
            ?? throw new InvalidOperationException($"Failed to load matcap texture at '{MatcapPath}'.");
    }

    public Material GetMaterial(int stencilReference)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(stencilReference);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(stencilReference, byte.MaxValue);

        if (_materials.TryGetValue(stencilReference, out StencilMaterial? existing))
            return existing.Material;

        Shader shader = StencilShader.CreateWriter(_sourceShader, stencilReference);
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("matcap", _matcap);
        var created = new StencilMaterial(shader, material);
        _materials.Add(stencilReference, created);
        return material;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (StencilMaterial material in _materials.Values)
            material.Dispose();
        _materials.Clear();
    }

    private sealed class StencilMaterial(Shader shader, ShaderMaterial material) : IDisposable
    {
        public ShaderMaterial Material { get; } = material;

        public void Dispose()
        {
            Material.Dispose();
            shader.Dispose();
        }
    }
}
