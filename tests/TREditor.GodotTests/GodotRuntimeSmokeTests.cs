using GdUnit4;
using gdUnit4.addons.gdUnit4.src.dotnet;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class GodotRuntimeSmokeTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void CanConstructGDMeshResource()
    {
        using var mesh = new GDMesh();

        AssertThat(mesh.ArrayMesh).IsNotNull();
        AssertThat(mesh.SpatialMesh).IsNotNull();
        AssertBool(GdUnit4CSharpApi.IsApiLoaded()).IsTrue();
    }
}
