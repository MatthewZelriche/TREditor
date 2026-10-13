using System;
using Godot;
using TRMesh.Mesh;

internal sealed class BridgeFacesCommand : IEditorCommand, IDisposable
{
    private const string StaleMessage =
        "The selected faces are missing or stale and cannot be bridged.";

    private readonly BridgeFacesPlan _plan;
    private readonly Vector3[][] _rings;
    private readonly IEditorSelection _selection;
    private readonly MeshObject[] _sources;
    private MeshPatch? _firstPatch;
    private MeshPatch? _secondPatch;
    private IEditorCommand? _selectionCommand;
    private MeshObject? _bridge;
    private int[]? _bridgeFaces;
    private bool _disposed;

    internal BridgeFacesCommand(
        BridgeFacesPlan plan,
        Vector3[][] rings,
        IEditorSelection selection
    )
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rings);
        ArgumentNullException.ThrowIfNull(selection);
        _plan = plan;
        _rings = rings;
        _selection = selection;
        _sources = ReferenceEquals(plan.First.Object, plan.Second.Object)
            ? [plan.First.Object]
            : [plan.First.Object, plan.Second.Object];
    }

    public string DisplayName => "Bridge Faces";

    public void Execute(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (_firstPatch is not null || _secondPatch is not null)
            throw new NonFatalCommandError(StaleMessage);

        if (_bridge is null)
        {
            GDMesh? bridgeMesh = null;
            try
            {
                SpatialMesh spatialMesh = BridgeFacesGeometry.BuildMesh(_rings, out int[] faces);
                bridgeMesh = new GDMesh(spatialMesh);
                MeshObject? added = document.EditGeometryAndAdd(
                    _sources,
                    "Bridge",
                    _plan.BridgeTransform,
                    bridgeMesh,
                    RecordSourceRemoval
                );
                if (added is null)
                    throw new NonFatalCommandError(StaleMessage);

                _bridge = added;
                bridgeMesh = null;
                _bridgeFaces = faces;
                CompleteExecute(document);
            }
            catch (ArgumentException exception)
            {
                bridgeMesh?.Dispose();
                throw new NonFatalCommandError(MeshPatchCommandBase.FirstLine(exception.Message));
            }
            catch
            {
                bridgeMesh?.Dispose();
                throw;
            }
            return;
        }

        try
        {
            if (
                !document.EditGeometryAndUpdateResult(
                    _sources,
                    _bridge,
                    addResult: true,
                    RecordSourceRemoval
                )
            )
                throw new NonFatalCommandError(StaleMessage);
            CompleteExecute(document);
        }
        catch (ArgumentException exception)
        {
            throw new NonFatalCommandError(MeshPatchCommandBase.FirstLine(exception.Message));
        }
    }

    public void Undo(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (_bridge is null || _firstPatch is null)
            throw new NonFatalCommandError(StaleMessage);

        if (
            !document.EditGeometryAndUpdateResult(
                _sources,
                _bridge,
                addResult: false,
                RevertSourceRemoval
            )
        )
            throw new NonFatalCommandError(StaleMessage);

        _selectionCommand!.Undo(document);
        _selectionCommand = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_firstPatch is null)
            _bridge?.Mesh.Dispose();
        _bridge = null;
    }

    private void CompleteExecute(Document document)
    {
        MeshObjectId bridgeId = _bridge!.Id;
        MeshComponentId[] components = Array.ConvertAll(
            _bridgeFaces!,
            face => new MeshComponentId(bridgeId, MeshComponentType.Face, face)
        );
        _selectionCommand = _selection.CreateCommand(SelectionValue.ForSelections([], components));
        _selectionCommand.Execute(document);
    }

    private void RecordSourceRemoval()
    {
        SpatialMesh firstMesh = _plan.First.Object.Mesh.SpatialMesh;
        if (ReferenceEquals(_plan.First.Object, _plan.Second.Object))
        {
            _firstPatch = firstMesh.Record(mesh =>
                mesh.DeleteFaces([_plan.First.Face, _plan.Second.Face])
            );
            return;
        }

        _firstPatch = firstMesh.Record(mesh => mesh.DeleteFaces([_plan.First.Face]));
        try
        {
            _secondPatch = _plan.Second.Object.Mesh.SpatialMesh.Record(mesh =>
                mesh.DeleteFaces([_plan.Second.Face])
            );
        }
        catch
        {
            firstMesh.Revert(_firstPatch);
            _firstPatch = null;
            throw;
        }
    }

    private void RevertSourceRemoval()
    {
        if (_firstPatch is null)
            throw new InvalidOperationException(StaleMessage);

        if (_secondPatch is not null)
        {
            _plan.Second.Object.Mesh.SpatialMesh.Revert(_secondPatch);
            _secondPatch = null;
        }
        _plan.First.Object.Mesh.SpatialMesh.Revert(_firstPatch);
        _firstPatch = null;
    }
}
