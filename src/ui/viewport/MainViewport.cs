#nullable enable

using Godot;

public partial class MainViewport : SubViewportContainer
{
    private static readonly Vector3 InitialCameraPosition = new(10.0f, 10.0f, 10.0f);
    private static readonly Vector3 InitialCameraTarget = Vector3.Zero;

    public MainViewport()
    {
        Name = "Perspective";
        Stretch = true;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        SubViewport renderViewport = new()
        {
            Name = "RenderViewport",
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible,
        };
        AddChild(renderViewport);

        Camera3D camera = new()
        {
            Name = "Camera",
            Projection = Camera3D.ProjectionType.Perspective,
            Current = true,
        };
        camera.LookAtFromPosition(InitialCameraPosition, InitialCameraTarget, Vector3.Up);
        renderViewport.AddChild(camera);
    }
}
