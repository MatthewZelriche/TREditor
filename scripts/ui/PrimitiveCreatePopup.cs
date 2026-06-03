using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector3 = System.Numerics.Vector3;

public partial class PrimitiveCreatePopup : PopupPanel
{
    private enum PrimitiveType
    {
        Box = 0,
        Cylinder = 1,
        Sphere = 2,
        Plane = 3,
    }

    private EditorSession _session;
    private OptionButton _primitiveTypeOption;
    private Control _boxSettings;
    private Control _cylinderSettings;
    private Control _sphereSettings;
    private Control _planeSettings;
    private Button _createButton;
    private bool _nodesCached;
    private bool _signalsWired;

    public override void _Ready()
    {
        CacheSceneNodes();
        WireSceneNodes();
        ShowPrimitiveSettings(_primitiveTypeOption.Selected);
    }

    public void ToggleBeside(Control anchor)
    {
        if (anchor == null)
        {
            return;
        }

        if (Visible)
        {
            Hide();
            return;
        }

        Vector2 popupPosition = anchor.GlobalPosition + new Vector2(anchor.Size.X, 0.0f);
        Position = new Vector2I(
            Mathf.RoundToInt(popupPosition.X),
            Mathf.RoundToInt(popupPosition.Y)
        );
        Popup();
    }

    private void CacheSceneNodes()
    {
        if (_nodesCached)
        {
            return;
        }

        _primitiveTypeOption = GetNode<OptionButton>("Margin/Column/PrimitiveTypeOption");
        _boxSettings = GetNode<Control>("Margin/Column/Settings/BoxSettings");
        _cylinderSettings = GetNode<Control>("Margin/Column/Settings/CylinderSettings");
        _sphereSettings = GetNode<Control>("Margin/Column/Settings/SphereSettings");
        _planeSettings = GetNode<Control>("Margin/Column/Settings/PlaneSettings");
        _createButton = GetNode<Button>("Margin/Column/Actions/CreateButton");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _nodesCached = true;
    }

    private void WireSceneNodes()
    {
        if (_signalsWired)
        {
            return;
        }

        _primitiveTypeOption.ItemSelected += ShowPrimitiveSettings;
        _createButton.Pressed += OnCreatePressed;

        _signalsWired = true;
    }

    private void ShowPrimitiveSettings(long index)
    {
        _boxSettings.Visible = index == 0;
        _cylinderSettings.Visible = index == 1;
        _sphereSettings.Visible = index == 2;
        _planeSettings.Visible = index == 3;
    }

    private void OnCreatePressed()
    {
        if (_session == null)
        {
            GD.PushWarning("PrimitiveCreatePopup could not find EditorSession.");
            return;
        }

        PrimitiveType primitiveType = (PrimitiveType)_primitiveTypeOption.Selected;
        if (TryBeginInteractiveCreation(primitiveType))
        {
            Hide();
            return;
        }

        SpatialMesh mesh = BuildPrimitiveMesh(primitiveType);
        _session.Commands.Execute(
            new CreateMeshCommand(_session, mesh, GetPrimitiveDisplayName(primitiveType))
        );
        Hide();
    }

    private bool TryBeginInteractiveCreation(PrimitiveType primitiveType)
    {
        switch (primitiveType)
        {
            case PrimitiveType.Box:
                _session.BeginPrimitiveCreation(PrimitiveCreationSettings.Box());
                return true;
            case PrimitiveType.Cylinder:
                _session.BeginPrimitiveCreation(
                    PrimitiveCreationSettings.Cylinder(
                        GetInt("Margin/Column/Settings/CylinderSettings/Sides")
                    )
                );
                return true;
            default:
                return false;
        }
    }

    private SpatialMesh BuildPrimitiveMesh(PrimitiveType primitiveType)
    {
        return primitiveType switch
        {
            PrimitiveType.Sphere => BuildSphereMesh(),
            PrimitiveType.Plane => BuildPlaneMesh(),
            _ => throw new System.ArgumentOutOfRangeException(
                nameof(primitiveType),
                primitiveType,
                null
            ),
        };
    }

    private SpatialMesh BuildSphereMesh()
    {
        return MeshBuilders.Build(
            new UvSphereOptions
            {
                Center = NumericsVector3.Zero,
                Radius = GetFloat("Margin/Column/Settings/SphereSettings/Radius"),
                LonSegments = GetInt("Margin/Column/Settings/SphereSettings/Segments"),
                LatSegments = GetInt("Margin/Column/Settings/SphereSettings/Rings"),
            }
        );
    }

    private SpatialMesh BuildPlaneMesh()
    {
        int subdivisions = GetInt("Margin/Column/Settings/PlaneSettings/Subdivisions");

        return MeshBuilders.Build(
            new PlaneOptions
            {
                Center = NumericsVector3.Zero,
                Width = GetFloat("Margin/Column/Settings/PlaneSettings/Width"),
                Height = GetFloat("Margin/Column/Settings/PlaneSettings/Depth"),
                WidthSegments = subdivisions,
                HeightSegments = subdivisions,
            }
        );
    }

    private static string GetPrimitiveDisplayName(PrimitiveType primitiveType)
    {
        return primitiveType switch
        {
            PrimitiveType.Box => "Box",
            PrimitiveType.Cylinder => "Cylinder",
            PrimitiveType.Sphere => "Sphere",
            PrimitiveType.Plane => "Plane",
            _ => "Primitive",
        };
    }

    private float GetFloat(NodePath spinBoxPath) => (float)GetNode<SpinBox>(spinBoxPath).Value;

    private int GetInt(NodePath spinBoxPath) =>
        Mathf.RoundToInt((float)GetNode<SpinBox>(spinBoxPath).Value);
}
