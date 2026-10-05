using System;
using Godot;

internal static class StencilShader
{
    public static Shader CreateDepthDrawNever(Shader source) =>
        CreateVariant(source, "depth_draw_never", null, transparent: false);

    public static Shader CreateWriter(Shader source, int reference) =>
        CreateVariant(source, null, $"write, compare_always, {reference}", transparent: false);

    public static Shader CreateXRayReader(Shader source, int reference) =>
        CreateVariant(
            source,
            "depth_test_disabled",
            $"read, compare_equal, {reference}",
            transparent: true
        );

    public static Shader CreateDepthTestDisabled(Shader source) =>
        CreateVariant(source, "depth_test_disabled", null, transparent: true);

    private static Shader CreateVariant(
        Shader source,
        string? additionalRenderMode,
        string? stencilMode,
        bool transparent
    )
    {
        string code = source.Code;
        int renderMode = code.IndexOf("render_mode ", StringComparison.Ordinal);
        int renderModeEnd = renderMode < 0
            ? -1
            : code.IndexOf(';', renderMode);
        if (renderModeEnd < 0)
            throw new InvalidOperationException("A stencil shader source must declare render_mode.");

        if (additionalRenderMode is not null)
        {
            code = code.Insert(renderModeEnd, $", {additionalRenderMode}");
            renderModeEnd += additionalRenderMode.Length + 2;
        }

        if (stencilMode is not null)
            code = code.Insert(renderModeEnd + 1, $"\nstencil_mode {stencilMode};");
        if (transparent && !code.Contains("ALPHA =", StringComparison.Ordinal))
        {
            const string FragmentDeclaration = "void fragment() {";
            int fragment = code.IndexOf(FragmentDeclaration, StringComparison.Ordinal);
            if (fragment < 0)
                throw new InvalidOperationException("An x-ray stencil shader must declare fragment().");
            code = code.Insert(fragment + FragmentDeclaration.Length, "\n\tALPHA = 1.0;");
        }

        return new Shader { Code = code };
    }
}
