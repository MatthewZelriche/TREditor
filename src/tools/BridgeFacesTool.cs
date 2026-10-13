using System;
using Godot;

public sealed partial class BridgeFacesTool
    : ToolBase,
        IEditorToolSettingsProvider,
        IEditorToolPrimaryAction
{
    public const string ToolId = "tool.bridge_faces";
    public const int DefaultIntermediateSteps = 3;
    public const int MaximumIntermediateSteps = 64;

    private int _intermediateSteps = DefaultIntermediateSteps;
    private BridgeFacesPlan? _plan;
    private Curve3D? _curve;

    public BridgeFacesTool(SelectionModeState? selectionMode = null)
        : base(selectionMode) { }

    public override string Id => ToolId;

    public string PrimaryActionText => "Apply Bridge";

    public int IntermediateSteps
    {
        get => _intermediateSteps;
        set
        {
            if (value < 0 || value > MaximumIntermediateSteps)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_intermediateSteps == value)
                return;
            _intermediateSteps = value;
            PrimaryActionChanged?.Invoke();
        }
    }

    internal Curve3D? Curve => _curve;

    internal BridgeFacesPlan? Plan => _plan;

    public event Action? PrimaryActionChanged;

    public Control CreateSettingsControl(IEditorToolContext context)
    {
        EnsureSession(context);
        return new NumericToolSettings(
            this,
            "BridgeFacesSettings",
            "Bridge Faces",
            "IntermediateSteps",
            "Intermediate steps",
            new SpinBox
            {
                Name = "IntermediateStepsSpin",
                MinValue = 0,
                MaxValue = MaximumIntermediateSteps,
                Step = 1,
                AllowGreater = false,
                AllowLesser = false,
            },
            () => IntermediateSteps,
            value => IntermediateSteps = (int)value
        );
    }

    public EditorToolAction CreatePrimaryAction(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BridgeFacesPlan plan;
        try
        {
            plan = BridgeFacesGeometry.Resolve(context.Document, context.Selection.Current);
        }
        catch (ArgumentException exception)
        {
            ResetSession(context);
            return new EditorToolAction(null, MeshPatchCommandBase.FirstLine(exception.Message));
        }

        EnsureSession(context, plan);
        try
        {
            Vector3[][] rings = BridgeFacesGeometry.CreateRings(plan, _curve!, IntermediateSteps);
            UpdatePreview(context, rings);
            return new EditorToolAction(
                new BridgeFacesCommand(plan, rings, context.Selection),
                null
            );
        }
        catch (ArgumentException exception)
        {
            ClearPreview(context);
            return new EditorToolAction(null, MeshPatchCommandBase.FirstLine(exception.Message));
        }
    }

    protected override bool TryBeginInteraction(
        IEditorToolContext context,
        EditorMouseInput input
    )
    {
        if (_curve is null)
            EnsureSession(context);
        if (_curve is null)
            return false;

        if (
            context.TranslateGizmos.IsTransientTranslation
            && context.TranslateGizmos.BeginInteraction(input.Camera, input.ScreenPosition)
        )
        {
            return true;
        }

        BezierCurveGizmo curveGizmo = context.BezierCurveGizmo;
        bool consumed = curveGizmo.BeginInteraction(input.Camera, input.ScreenPosition);
        if (
            consumed
            && curveGizmo.SelectedHandle
                == new BezierCurveHandle(1, BezierCurveHandleKind.Anchor)
        )
        {
            // Selecting the control point reveals the standard translation gizmo. End the Bezier
            // camera-plane drag before it can move so all subsequent movement is explicit.
            if (curveGizmo.IsInteracting)
                curveGizmo.EndInteraction();
            ShowControlPointGizmo(context);
        }
        else
        {
            context.TranslateGizmos.HideTransientTranslation();
        }

        return consumed;
    }

    public override void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        if (_curve is null || _plan is null)
            return;
        if (context.TranslateGizmos.IsInteracting)
        {
            if (
                !context.TranslateGizmos.UpdateInteraction(
                    input.Camera,
                    input.ScreenPosition
                )
            )
            {
                return;
            }

            _curve.SetPointPosition(
                1,
                _plan.BridgeTransform.AffineInverse()
                    * context.TranslateGizmos.ProxyTransform.Origin
            );
            BridgeFacesGeometry.SynchronizeQuadraticTangents(_curve);
            RefreshPreview(context);
            return;
        }
        if (!context.BezierCurveGizmo.IsInteracting)
            return;
        if (
            !context.BezierCurveGizmo.UpdateInteraction(
                input.Camera,
                input.ScreenPosition,
                fineMovement: input.IsShiftPressed,
                snapping: input.IsCtrlPressed
            )
        )
        {
            return;
        }

        BridgeFacesGeometry.SynchronizeQuadraticTangents(_curve);
        RefreshPreview(context);
    }

    public override void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        _ = input;
        if (context.TranslateGizmos.IsInteracting)
        {
            context.TranslateGizmos.EndInteraction();
            return;
        }
        if (_curve is not null && context.BezierCurveGizmo.IsInteracting)
            context.BezierCurveGizmo.EndInteraction();
    }

    protected override void OnToolMouseMove(IEditorToolContext context, EditorMouseInput input)
    {
        if (_curve is not null)
            context.BezierCurveGizmo.UpdateHover(input.Camera, input.ScreenPosition);
    }

    protected override void OnCancel(IEditorToolContext context) => ResetSession(context);

    private void EnsureSession(IEditorToolContext context)
    {
        try
        {
            EnsureSession(
                context,
                BridgeFacesGeometry.Resolve(context.Document, context.Selection.Current)
            );
        }
        catch (ArgumentException)
        {
            ResetSession(context);
        }
    }

    private void EnsureSession(IEditorToolContext context, BridgeFacesPlan plan)
    {
        if (
            _curve is not null
            && _plan is not null
            && ReferenceEquals(_plan.First.Object, plan.First.Object)
            && ReferenceEquals(_plan.Second.Object, plan.Second.Object)
            && _plan.First.Face == plan.First.Face
            && _plan.Second.Face == plan.Second.Face
        )
            return;

        ResetSession(context);
        _plan = plan;
        _curve = BridgeFacesGeometry.CreateQuadraticCurve(plan);
        BezierCurveGizmo gizmo = context.BezierCurveGizmo;
        gizmo.Show(_curve, plan.BridgeTransform);
        for (int point = 0; point < _curve.PointCount; point++)
        {
            gizmo.SetHandleLocked(
                new BezierCurveHandle(point, BezierCurveHandleKind.Anchor),
                point != 1
            );
            if (point > 0)
            {
                gizmo.SetHandleLocked(
                    new BezierCurveHandle(point, BezierCurveHandleKind.Incoming),
                    true
                );
            }
            if (point + 1 < _curve.PointCount)
            {
                gizmo.SetHandleLocked(
                    new BezierCurveHandle(point, BezierCurveHandleKind.Outgoing),
                    true
                );
            }
        }
        RefreshPreview(context);
    }

    private void RefreshPreview(IEditorToolContext context)
    {
        if (_plan is null || _curve is null)
            return;
        try
        {
            UpdatePreview(
                context,
                BridgeFacesGeometry.CreateRings(_plan, _curve, IntermediateSteps)
            );
        }
        catch (ArgumentException)
        {
            ClearPreview(context);
        }
        PrimaryActionChanged?.Invoke();
    }

    private void UpdatePreview(IEditorToolContext context, Vector3[][] rings)
    {
        var mesh = new GDMesh(BridgeFacesGeometry.BuildMesh(rings, out _));
        context.Preview.SetGeometry(mesh, _plan!.BridgeTransform);
    }

    private void ResetSession(IEditorToolContext context)
    {
        context.TranslateGizmos.HideTransientTranslation();
        if (_curve is not null && ReferenceEquals(context.BezierCurveGizmo.Curve, _curve))
        {
            context.BezierCurveGizmo.Hide();
        }
        ClearPreview(context);
        _curve?.Dispose();
        _curve = null;
        _plan = null;
    }

    private static void ClearPreview(IEditorToolContext context) =>
        context.Preview.ClearGeometry();

    private void ShowControlPointGizmo(IEditorToolContext context)
    {
        Vector3 worldPosition = _plan!.BridgeTransform * _curve!.GetPointPosition(1);
        context.TranslateGizmos.ShowTransientTranslationAt(
            new Transform3D(Basis.Identity, worldPosition)
        );
    }

}
