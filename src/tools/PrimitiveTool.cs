using System;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

public enum PrimitiveShape
{
    Box,
    Cylinder,
}

// Creates a primitive in two stages: drag its footprint on a picked plane, then move the pointer
// vertically to choose extrusion height and click once to commit.
public sealed partial class PrimitiveTool : IEditorTool, IEditorToolSettingsProvider
{
    public const string ToolId = "tool.primitive";
    public const int MinimumCylinderSides = 3;
    public const int MaximumCylinderSides = 64;
    private const float MinimumDimension = 0.001f;
    private const float FlatPreviewThickness = 0.01f;
    private const float HeightUnitsPerPixel = 0.02f;

    private InteractionPhase _phase;
    private Camera3D? _sourceCamera;
    private Vector3 _planeOrigin;
    private Vector3 _planeNormal;
    private Vector3 _planeXAxis;
    private Vector3 _planeZAxis;
    private Vector3 _baseStart;
    private Vector3 _baseCenter;
    private Vector2 _baseSize;
    private Vector2 _heightStartScreen;
    private float _signedHeight;
    private PrimitiveShape _shape;
    private int _cylinderSides = 16;

    public string Id => ToolId;

    public PrimitiveShape Shape
    {
        get => _shape;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_shape == value)
                return;

            _shape = value;
            SettingsChanged?.Invoke();
        }
    }

    public int CylinderSides
    {
        get => _cylinderSides;
        set
        {
            if (value is < MinimumCylinderSides or > MaximumCylinderSides)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_cylinderSides == value)
                return;

            _cylinderSides = value;
            SettingsChanged?.Invoke();
        }
    }

    private event Action? SettingsChanged;

    internal bool IsDefiningBase => _phase == InteractionPhase.DefiningBase;

    internal bool IsDefiningHeight => _phase == InteractionPhase.DefiningHeight;

    public Control CreateSettingsControl() => new SettingsControl(this);

    public void OnMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (input.Camera is null)
            return;

        if (_phase == InteractionPhase.DefiningHeight)
        {
            if (ReferenceEquals(input.Camera, _sourceCamera))
                Commit(context);
            return;
        }

        if (_phase != InteractionPhase.Idle)
            Cancel(context);

        if (!TryFindBasePlane(context, input, out Vector3 point, out Vector3 normal))
            return;

        _sourceCamera = input.Camera;
        _planeOrigin = point;
        _planeNormal = normal.Normalized();
        BuildPlaneAxes(_planeNormal, out _planeXAxis, out _planeZAxis);
        _baseStart = point;
        _baseCenter = point;
        _baseSize = Vector2.Zero;
        _signedHeight = 0.0f;
        _phase = InteractionPhase.DefiningBase;
    }

    public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (
            _phase != InteractionPhase.DefiningBase
            || !ReferenceEquals(input.Camera, _sourceCamera)
        )
        {
            return;
        }

        UpdateBase(context, input.ScreenPosition);
    }

    public void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (
            _phase != InteractionPhase.DefiningBase
            || !ReferenceEquals(input.Camera, _sourceCamera)
        )
        {
            return;
        }

        UpdateBase(context, input.ScreenPosition);
        if (!HasValidBase())
        {
            Cancel(context);
            return;
        }

        _heightStartScreen = input.ScreenPosition;
        _signedHeight = 0.0f;
        _phase = InteractionPhase.DefiningHeight;
        UpdatePreview(context);
    }

    public void OnMouseMove(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (
            _phase != InteractionPhase.DefiningHeight
            || !ReferenceEquals(input.Camera, _sourceCamera)
        )
        {
            return;
        }

        _signedHeight = (_heightStartScreen.Y - input.ScreenPosition.Y) * HeightUnitsPerPixel;
        UpdatePreview(context);
    }

    public void Cancel(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ResetState();
        context.Preview.ClearGeometry();
    }

    private bool TryFindBasePlane(
        IEditorToolContext context,
        EditorMouseInput input,
        out Vector3 point,
        out Vector3 normal
    )
    {
        if (context.Picking.TryRaycast(input.Camera, input.ScreenPosition, out PickingHit hit))
        {
            point = hit.Position;
            normal = hit.Normal;
            return !normal.IsZeroApprox();
        }

        normal = Vector3.Up;

        // TODO: Will want to move this to PickingService at some point
        return TryIntersectPlane(input.Camera, input.ScreenPosition, Vector3.Zero, normal, out point);
    }

    private void UpdateBase(IEditorToolContext context, Vector2 screenPosition)
    {
        if (
            _sourceCamera is null
            || !TryIntersectPlane(
                _sourceCamera,
                screenPosition,
                _planeOrigin,
                _planeNormal,
                out Vector3 current
            )
        )
        {
            return;
        }

        Vector3 delta = current - _baseStart;
        float width = delta.Dot(_planeXAxis);
        float depth = delta.Dot(_planeZAxis);
        _baseSize = new Vector2(Mathf.Abs(width), Mathf.Abs(depth));
        _baseCenter = _baseStart + (_planeXAxis * width * 0.5f) + (_planeZAxis * depth * 0.5f);
        if (HasValidBase())
            UpdatePreview(context);
        else
            context.Preview.ClearGeometry();
    }

    private void UpdatePreview(IEditorToolContext context)
    {
        if (!HasValidBase())
            return;

        float height = Mathf.Max(Mathf.Abs(_signedHeight), FlatPreviewThickness);
        float direction = _signedHeight < 0.0f ? -1.0f : 1.0f;
        GDMesh mesh = BuildMesh(height);
        context.Preview.SetGeometry(mesh, BuildTransform(height, direction));
    }

    private void Commit(IEditorToolContext context)
    {
        if (!HasValidBase() || Mathf.Abs(_signedHeight) < MinimumDimension)
        {
            Cancel(context);
            return;
        }

        float height = Mathf.Abs(_signedHeight);
        float direction = Mathf.Sign(_signedHeight);
        var command = new CreatePrimitiveCommand(
            _shape == PrimitiveShape.Box ? "Box" : "Cylinder",
            BuildTransform(height, direction),
            BuildMesh(height)
        );
        try
        {
            context.Commands.Submit(command);
        }
        catch
        {
            command.Dispose();
            throw;
        }
        finally
        {
            ResetState();
            context.Preview.ClearGeometry();
        }
    }

    private GDMesh BuildMesh(float height)
    {
        if (_shape == PrimitiveShape.Cylinder)
        {
            float diameter = Mathf.Min(_baseSize.X, _baseSize.Y);
            return new GDMesh(SpatialMeshBuilder.CreateCylinder(height, diameter, _cylinderSides));
        }

        return new GDMesh(
            SpatialMeshBuilder.CreateAabb(
                new NumericsVector3(-_baseSize.X * 0.5f, 0.0f, -_baseSize.Y * 0.5f),
                new NumericsVector3(_baseSize.X, height, _baseSize.Y)
            )
        );
    }

    private Transform3D BuildTransform(float height, float direction)
    {
        Vector3 yAxis = _planeNormal * direction;
        Vector3 zAxis = _planeZAxis * direction;
        var basis = new Basis(_planeXAxis, yAxis, zAxis);
        Vector3 origin = _baseCenter;
        if (_shape == PrimitiveShape.Cylinder)
            origin += _planeNormal * direction * height * 0.5f;

        return new Transform3D(basis, origin);
    }

    private bool HasValidBase() =>
        _baseSize.X >= MinimumDimension && _baseSize.Y >= MinimumDimension;

    private void ResetState()
    {
        _phase = InteractionPhase.Idle;
        _sourceCamera = null;
        _baseSize = Vector2.Zero;
        _signedHeight = 0.0f;
    }

    private static void BuildPlaneAxes(Vector3 normal, out Vector3 xAxis, out Vector3 zAxis)
    {
        xAxis = Vector3.Right - (normal * normal.Dot(Vector3.Right));
        if (xAxis.IsZeroApprox())
            xAxis = Vector3.Forward - (normal * normal.Dot(Vector3.Forward));

        xAxis = xAxis.Normalized();
        zAxis = xAxis.Cross(normal).Normalized();
    }

    private static bool TryIntersectPlane(
        Camera3D camera,
        Vector2 screenPosition,
        Vector3 planePoint,
        Vector3 planeNormal,
        out Vector3 intersection
    )
    {
        Vector3 rayOrigin = camera.ProjectRayOrigin(screenPosition);
        Vector3 rayDirection = camera.ProjectRayNormal(screenPosition);
        float denominator = planeNormal.Dot(rayDirection);
        if (Mathf.IsZeroApprox(denominator))
        {
            intersection = default;
            return false;
        }

        float distance = planeNormal.Dot(planePoint - rayOrigin) / denominator;
        if (!float.IsFinite(distance) || distance < 0.0f)
        {
            intersection = default;
            return false;
        }

        intersection = rayOrigin + (rayDirection * distance);
        return intersection.IsFinite();
    }

    private enum InteractionPhase
    {
        Idle,
        DefiningBase,
        DefiningHeight,
    }

    // The tool-specific settings UI lives with the tool. The sidebar treats this as an opaque
    // control and owns only its placement and lifetime.
    private sealed partial class SettingsControl : VBoxContainer
    {
        private readonly PrimitiveTool _tool;
        private readonly OptionButton _shapeOption;
        private readonly HBoxContainer _sidesRow;
        private readonly SpinBox _sidesSpin;
        private bool _syncing;

        public SettingsControl(PrimitiveTool tool)
        {
            _tool = tool;
            AddThemeConstantOverride("separation", 8);
            AddChild(new Label { Text = "Primitive" });

            _shapeOption = new OptionButton
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _shapeOption.AddItem("Box", (int)PrimitiveShape.Box);
            _shapeOption.AddItem("Cylinder", (int)PrimitiveShape.Cylinder);
            _shapeOption.ItemSelected += OnShapeSelected;
            AddChild(_shapeOption);

            _sidesRow = new HBoxContainer();
            _sidesRow.AddChild(
                new Label
                {
                    Text = "Sides",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                }
            );
            _sidesSpin = new SpinBox
            {
                MinValue = MinimumCylinderSides,
                MaxValue = MaximumCylinderSides,
                Step = 1.0,
                AllowGreater = false,
                AllowLesser = false,
            };
            _sidesSpin.ValueChanged += OnSidesChanged;
            _sidesRow.AddChild(_sidesSpin);
            AddChild(_sidesRow);

            _tool.SettingsChanged += Sync;
            Sync();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
                _tool.SettingsChanged -= Sync;
        }

        private void Sync()
        {
            _syncing = true;
            _shapeOption.Select((int)_tool.Shape);
            _sidesSpin.Value = _tool.CylinderSides;
            _sidesRow.Visible = _tool.Shape == PrimitiveShape.Cylinder;
            _syncing = false;
        }

        private void OnShapeSelected(long index)
        {
            if (!_syncing)
            {
                _tool.Shape = (PrimitiveShape)_shapeOption.GetItemId((int)index);
            }
        }

        private void OnSidesChanged(double value)
        {
            if (!_syncing)
                _tool.CylinderSides = Mathf.RoundToInt(value);
        }
    }
}
