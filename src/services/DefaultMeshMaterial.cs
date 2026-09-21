using System;
using Godot;

// Shared unlit matcap used as the default surface look for committed scene meshes.
internal sealed class DefaultMeshMaterial : IDisposable
{
    public const string ShaderPath = "res://src/shaders/matcap_unlit.gdshader";
    public const string MatcapPath = "res://assets/matcaps/matcap-cool-bluegrey.png";

    private readonly ShaderMaterial _material;
    private bool _disposed;

    public DefaultMeshMaterial()
    {
        var shader = GD.Load<Shader>(ShaderPath)
            ?? throw new InvalidOperationException($"Failed to load matcap shader at '{ShaderPath}'.");
        var matcap = GD.Load<Texture2D>(MatcapPath)
            ?? throw new InvalidOperationException($"Failed to load matcap texture at '{MatcapPath}'.");

        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("matcap", matcap);
    }

    public Material Material
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _material;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _material.Dispose();
    }
}
