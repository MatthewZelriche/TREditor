#nullable enable

using Godot;

public sealed class TextureTool : IEditorTool
{
    private readonly EditorToolContext _context;

    public TextureTool(EditorToolContext context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public void Enter()
    {
        _context.ComponentSelectionHighlight.SetMode(ComponentHighlightMode.FaceHoverOnly);
        _context.ComponentSelectionHighlight.SetActive(true);
        ReportActiveTextureStatus();
    }

    public void Exit()
    {
        _context.ComponentSelectionHighlight.SetActive(false);
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input)
    {
        ScenePickHit hit = PickFace(input.RayOrigin, input.RayDirection);
        UpdateHover(input.RayOrigin, hit);
        if (input.Button == MouseButton.Left && input.Pressed)
            ReportActiveTextureStatus();

        if (
            !TextureToolInput.TryResolveApplyIntent(
                input,
                _context.TextureCatalog.ActiveAssetId,
                hit,
                out TextureFaceApplyIntent intent
            )
        )
        {
            return EditorToolResult.Continue;
        }

        int materialSlot = _context.TextureMaterials.GetOrCreateSlot(intent.AssetId);
        TRMeshGD meshNode = _context.GetMeshNode(hit.ObjectId);
        ApplyTextureToFaceCommand? command =
            meshNode != null
                ? ApplyTextureToFaceCommand.CreateIfValid(
                    hit.ObjectId,
                    meshNode.SourceMesh,
                    intent.Face,
                    materialSlot
                )
                : null;
        return command == null
            ? EditorToolResult.Continue
            : EditorToolResult.ContinueWithCommand(command);
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        UpdateHover(input.RayOrigin, PickFace(input.RayOrigin, input.RayDirection));
        return EditorToolResult.Continue;
    }

    public EditorToolResult HandleAction(EditorInputAction action) => EditorToolResult.Continue;

    public EditorToolResult Cancel() => EditorToolResult.Cancelled();

    private ScenePickHit PickFace(Vector3 rayOrigin, Vector3 rayDirection)
    {
        return _context.ScenePicking.TryPickScene(
            rayOrigin,
            rayDirection,
            out ScenePickHit hit,
            ScenePickElementFilter.Face,
            xRayMode: false
        )
            ? hit
            : ScenePickHit.None;
    }

    private void UpdateHover(Vector3 cameraOrigin, ScenePickHit hit)
    {
        SelectionTarget? hover = SelectionTarget.TryFromHit(hit, out SelectionTarget target)
            ? target
            : null;
        _context.ComponentSelectionHighlight.SetPointerState(cameraOrigin, hover);
    }

    private void ReportActiveTextureStatus()
    {
        string? activeAssetId = _context.TextureCatalog.ActiveAssetId;
        QueuedResourceState? previewState =
            activeAssetId != null
            && _context.TextureCatalog.TryGetPreview(activeAssetId, out var preview)
                ? preview.State
                : null;
        _context.ReportStatus(
            TextureToolStatus.GetActiveTextureMessage(activeAssetId, previewState)
        );
    }
}
