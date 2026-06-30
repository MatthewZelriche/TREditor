using Godot;

public sealed class EditTool : IEditorTool
{
    // First-pass default; later this should be driven by an Edit-mode UI toggle.
    private const bool XRayModeEnabled = true;

    private readonly EditorToolContext _context;
    private readonly EdgeCutToolInput _edgeCutInput;

    public EditTool(EditorToolContext context)
    {
        _context = context;
        _edgeCutInput = new EdgeCutToolInput(context);
    }

    public void Enter()
    {
        _context.ComponentSelectionHighlight.SetMode(ComponentHighlightMode.Edit);
        _context.ComponentSelectionHighlight.SetActive(true);
        _context.SelectionTranslationGizmo.SetFaceExtrusionEnabled(true);
        _context.SelectionTranslationGizmo.SetActive(true);
        _context.EditOperationSettings.Changed += OnEditOperationChanged;
        OnEditOperationChanged();
    }

    public void Exit()
    {
        _context.ComponentSelectionHighlight.SetActive(false);
        _context.SelectionTranslationGizmo.SetActive(false);
        _context.SelectionTranslationGizmo.SetFaceExtrusionEnabled(false);
        _context.EditOperationSettings.Changed -= OnEditOperationChanged;
        _edgeCutInput.Reset();
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input)
    {
        if (_context.EditOperationSettings.IsSelected("EdgeCut"))
            return _edgeCutInput.HandleMouseButton(input);

        if (
            _context.EditOperationSettings.IsSelected("InsetFace")
            || _context.EditOperationSettings.IsSelected("BevelEdge")
            || _context.EditOperationSettings.IsSelected("BevelVertex")
            || _context.EditOperationSettings.IsSelected("CollapseVertices")
            || _context.EditOperationSettings.IsSelected("BridgeEdges")
            || _context.EditOperationSettings.IsSelected("DetachFace")
            || _context.EditOperationSettings.IsSelected("FillHole")
            || _context.EditOperationSettings.IsSelected("CollapseFace")
        )
            return EditorToolResult.Continue;

        UpdateHover(input.RayOrigin, input.RayDirection);
        return SelectionToolInput.HandleMouseButton(
            _context,
            input,
            ScenePickElementFilter.AnyComponent,
            XRayModeEnabled
        );
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        if (_context.EditOperationSettings.IsSelected("EdgeCut"))
            return _edgeCutInput.HandleMouseMotion(input);

        UpdateHover(input.RayOrigin, input.RayDirection);
        return EditorToolResult.Continue;
    }

    public EditorToolResult HandleKey(Key key)
    {
        if (_context.EditOperationSettings.IsSelected("EdgeCut"))
            return _edgeCutInput.HandleKey(key);

        return key == Key.Delete
            ? EditorToolResult.ContinueWithCommand(CreateDeleteCommand())
            : EditorToolResult.Continue;
    }

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();

    // Prefer the lowest-dimensional selected component because its deletion cascades upward.
    private EditorCommand CreateDeleteCommand()
    {
        SelectionSnapshot selection = _context.Selection.Current;
        EditorCommand vertexDeletion = DeleteVertexCommand.CreateIfAny(selection);
        EditorCommand edgeDeletion = DeleteEdgeCommand.CreateIfAny(selection);
        return vertexDeletion ?? edgeDeletion ?? DeleteFaceCommand.CreateIfAny(selection);
    }

    private void UpdateHover(Vector3 rayOrigin, Vector3 rayDirection)
    {
        SelectionTarget? hover = null;
        if (
            _context.ScenePicking.TryPickScene(
                rayOrigin,
                rayDirection,
                out ScenePickHit hit,
                ScenePickElementFilter.AnyComponent,
                XRayModeEnabled
            ) && SelectionTarget.TryFromHit(hit, out SelectionTarget target)
        )
        {
            hover = target;
        }

        _context.ComponentSelectionHighlight.SetPointerState(rayOrigin, hover);
    }

    private void OnEditOperationChanged()
    {
        bool edgeCutSelected = _context.EditOperationSettings.IsSelected("EdgeCut");
        _context.ComponentSelectionHighlight.SetMode(
            edgeCutSelected
                ? ComponentHighlightMode.EditComponents(
                    ComponentHighlightKinds.Edges | ComponentHighlightKinds.Faces
                )
                : ComponentHighlightMode.Edit
        );
        if (!edgeCutSelected)
        {
            _edgeCutInput.Reset();
            _context.ReportStatus("");
        }
    }
}
