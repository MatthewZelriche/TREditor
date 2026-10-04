using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

// Draws component-mode feedback in batches. Each mesh object owns one vertex MultiMesh and one
// edge ArrayMesh; the number of Godot nodes therefore stays constant as topology grows.
public sealed class ComponentSelectionRenderService : ISceneService
{
    private const string VertexShaderPath = "res://src/shaders/component_vertex.gdshader";
    private const string ActiveEdgeShaderPath = "res://src/shaders/component_edge_active.gdshader";
    private const string FaceShaderPath = "res://src/shaders/component_face.gdshader";
    private const float VertexSizePixels = 8.0f;
    private const float ActiveVertexSizePixels = 12.0f;
    private const float ActiveEdgeWidthPixels = 5.0f;
    private const float ActiveEdgeCullMargin = 1.0f;
    // Component overlays share the transparent queue so these tiers strictly place every vertex
    // pass after every edge pass, while preserving selected-over-hovered-over-muted ordering.
    private enum ComponentRenderPriority
    {
        SelectedVertex = (int)Material.RenderPriorityMax,
        HoverVertex = SelectedVertex - 1,
        MutedVertex = SelectedVertex - 2,
        SelectedEdge = SelectedVertex - 3,
        HoverEdge = SelectedVertex - 4,
        MutedEdge = SelectedVertex - 5,
    }

    private static readonly Color MutedVertexColor = new(0.18f, 0.43f, 0.72f);
    private static readonly Color SelectedColor = new(1.0f, 0.86f, 0.08f);
    private static readonly Color MutedEdgeColor = new(0.16f, 0.36f, 0.62f);
    private static readonly Color HoverEdgeColor = new(0.28f, 0.58f, 0.9f);

    private readonly Node3D _root = new() { Name = "ComponentSelection" };
    private readonly Dictionary<MeshObjectId, VisualEntry> _entries = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private readonly PreviewState _previews;
    private readonly SelectionModeState _selectionMode;
    private readonly SelectionState _selection;
    private readonly MeshStencilReferences _stencilReferences;
    private readonly QuadMesh _vertexQuad;
    private readonly ShaderMaterial _vertexMaterial;
    private readonly Shader _selectedVertexShader;
    private readonly StandardMaterial3D _edgeMaterial;
    private readonly Shader _activeEdgeShader;
    private readonly Dictionary<int, Shader> _vertexXRayShaders = [];
    private readonly Dictionary<int, Shader> _edgeXRayShaders = [];
    private readonly ArrayMesh _activeEdgeMesh;
    private readonly ArrayMesh _hoverFaceMesh = new();
    private readonly ShaderMaterial _hoverFaceMaterial;
    private readonly ShaderMaterial _selectedFaceMaterial;
    private readonly List<(MeshInstance3D Instance, IDisposable Owned)> _selectedVisuals = [];
    private readonly HoverOverlay _hoverVertex;
    private readonly HoverOverlay _hoverEdge;
    private readonly MeshInstance3D _hoverFaceInstance;
    private Document _document;
    private bool _selectedVisualsDirty = true;
    private bool _disposed;

    internal ComponentSelectionRenderService(
        Document document,
        Node3D mapRoot,
        PreviewState previews,
        SelectionModeState selectionMode,
        SelectionState selection,
        MeshStencilReferences stencilReferences
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mapRoot);
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(selectionMode);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(stencilReferences);

        _document = document;
        _previews = previews;
        _selectionMode = selectionMode;
        _selection = selection;
        _stencilReferences = stencilReferences;

        Shader vertexShader = LoadShader(VertexShaderPath);
        _activeEdgeShader = LoadShader(ActiveEdgeShaderPath);
        Shader faceShader = LoadShader(FaceShaderPath);
        _selectedVertexShader = StencilShader.CreateDepthDrawNever(vertexShader);
        _vertexMaterial = new ShaderMaterial
        {
            Shader = vertexShader,
            RenderPriority = (int)ComponentRenderPriority.MutedVertex,
        };
        _vertexMaterial.SetShaderParameter("point_color", MutedVertexColor);
        _vertexMaterial.SetShaderParameter("point_size_pixels", VertexSizePixels);
        _vertexQuad = new QuadMesh
        {
            Size = Vector2.One,
            Material = _vertexMaterial,
        };
        _edgeMaterial = new StandardMaterial3D
        {
            AlbedoColor = MutedEdgeColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            RenderPriority = (int)ComponentRenderPriority.MutedEdge,
        };
        _activeEdgeMesh = CreateActiveEdgeMesh();
        _hoverFaceMaterial = CreateFaceMaterial(faceShader, new Color(1.0f, 0.43f, 0.08f, 0.22f));
        _selectedFaceMaterial = CreateFaceMaterial(faceShader, new Color(1.0f, 0.38f, 0.04f, 0.48f));

        _hoverVertex = new HoverOverlay(
            CreateOverlayInstance("HoveredVertex", _vertexQuad, null),
            stencilReference => CreateActiveVertexMaterial(
                stencilReference,
                MutedVertexColor,
                ActiveVertexSizePixels,
                ComponentRenderPriority.HoverVertex
            )
        );
        _hoverEdge = new HoverOverlay(
            CreateOverlayInstance("HoveredEdge", _activeEdgeMesh, null),
            stencilReference => CreateActiveEdgeMaterial(
                stencilReference,
                HoverEdgeColor,
                ActiveEdgeWidthPixels,
                ComponentRenderPriority.HoverEdge
            )
        );
        _hoverFaceInstance = CreateOverlayInstance("HoveredFace", _hoverFaceMesh, _hoverFaceMaterial);

        try
        {
            _root.AddChild(_hoverVertex.Instance);
            _root.AddChild(_hoverEdge.Instance);
            _root.AddChild(_hoverFaceInstance);
            mapRoot.AddChild(_root);
            Synchronize();
        }
        catch
        {
            DisposeResources();
            throw;
        }
    }

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (ReferenceEquals(_document, document))
            return;

        ClearEntries();
        _document = document;
        _selectedVisualsDirty = true;
        Synchronize();
    }

    public void OnDocumentChanged(DocumentChangeSet changeSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changeSet);
        DocumentObjectChange change = changeSet.Change;
        switch (change.Kind)
        {
            case DocumentChangeKind.Added:
                AddObject(change.ObjectId);
                break;
            case DocumentChangeKind.Removed:
                RemoveObject(change.ObjectId);
                break;
            case DocumentChangeKind.TransformChanged:
                ApplyTransform(change.ObjectId);
                break;
            case DocumentChangeKind.GeometryChanged:
                MarkGeometryDirty(change.ObjectId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changeSet), change.Kind, null);
        }
    }

    public void OnPreviewTransformChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplyTransform(id);
    }

    public void OnSelectionChanged() => InvalidateSelectedVisuals();

    public void OnSelectionHoverChanged() => Synchronize();

    public void OnSelectionModeChanged() => InvalidateSelectedVisuals();

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _selectedVisualsDirty |= _dirtyMeshes.Count != 0;
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (
                _document.TryGetObject(id, out MeshObject meshObject)
                && _entries.TryGetValue(id, out VisualEntry? entry)
            )
            {
                meshObject.Mesh.RebuildSurfaceIfDirty();
                entry.UpdateGeometry(meshObject.Mesh);
            }
        }

        _dirtyMeshes.Clear();
        Synchronize();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearEntries();
        DisposeResources();
    }

    private void AddObject(MeshObjectId id)
    {
        if (!_document.TryGetObject(id, out MeshObject meshObject))
            throw new InvalidOperationException($"Document object {id.Value} was added but is missing.");

        var entry = new VisualEntry(
            meshObject,
            _root,
            EffectiveTransform(meshObject),
            _vertexQuad,
            _edgeMaterial
        );
        _entries.Add(id, entry);
        _dirtyMeshes.Add(id);
        _selectedVisualsDirty = true;
        Synchronize();
    }

    private void RemoveObject(MeshObjectId id)
    {
        _dirtyMeshes.Remove(id);
        if (_entries.Remove(id, out VisualEntry? entry))
            entry.Dispose();
        _selectedVisualsDirty = true;
        Synchronize();
    }

    private void MarkGeometryDirty(MeshObjectId id)
    {
        if (_document.TryGetObject(id, out MeshObject meshObject))
        {
            meshObject.Mesh.MarkDirty();
            _dirtyMeshes.Add(id);
        }
    }

    private void ApplyTransform(MeshObjectId id)
    {
        if (
            _document.TryGetObject(id, out MeshObject meshObject)
            && _entries.TryGetValue(id, out VisualEntry? entry)
        )
        {
            entry.Root.Transform = EffectiveTransform(meshObject);
            Synchronize();
        }
    }

    private Transform3D EffectiveTransform(MeshObject meshObject) =>
        _previews.TryGetTransform(meshObject.Id, out Transform3D preview)
            ? preview
            : meshObject.Transform;

    private void Synchronize()
    {
        if (_disposed)
            return;

        ComponentMask mask = _selectionMode.Mode == EditorMode.Component
            ? _selectionMode.Components
            : ComponentMask.None;
        foreach (VisualEntry entry in _entries.Values)
            entry.SetVisibleComponents(mask);

        if (_selectedVisualsDirty)
        {
            ClearSelectedVisuals();
            foreach (MeshComponentId selected in _selection.Current.Components)
            {
                if (!Includes(mask, selected.Type))
                    continue;
                AddSelectedVisual(selected);
            }

            _selectedVisualsDirty = false;
        }

        MeshComponentId? hover = _selection.HoveredComponent is MeshComponentId hovered
            && Includes(mask, hovered.Type)
            && !_selection.Current.Components.Contains(hovered)
                ? hovered
                : null;
        SynchronizeHoveredComponent(hover);
    }

    private void InvalidateSelectedVisuals()
    {
        _selectedVisualsDirty = true;
        Synchronize();
    }

    private void AddSelectedVisual(MeshComponentId id)
    {
        if (!_entries.TryGetValue(id.ObjectId, out VisualEntry? entry))
            return;

        switch (id.Type)
        {
            case MeshComponentType.Vertex
                when entry.TryGetVertex(id.Slot, out Vector3 position):
                {
                    StencilMaterialPair material = CreateActiveVertexMaterial(
                        _stencilReferences.Get(id.ObjectId),
                        SelectedColor,
                        ActiveVertexSizePixels,
                        ComponentRenderPriority.SelectedVertex
                    );
                    MeshInstance3D instance = CreateOverlayInstance(
                        "SelectedVertex",
                        _vertexQuad,
                        material.Material
                    );
                    instance.Transform = new Transform3D(Basis.Identity, position);
                    AddSelectedVisual(entry, instance, material);
                    break;
                }
            case MeshComponentType.Edge
                when entry.TryGetEdge(id.Slot, out Vector3 start, out Vector3 end):
                {
                    StencilMaterialPair material = CreateActiveEdgeMaterial(
                        _stencilReferences.Get(id.ObjectId),
                        SelectedColor,
                        ActiveEdgeWidthPixels,
                        ComponentRenderPriority.SelectedEdge
                    );
                    MeshInstance3D instance = CreateOverlayInstance(
                        "SelectedEdge",
                        _activeEdgeMesh,
                        material.Material
                    );
                    ConfigureActiveEdge(instance, material, start, end);
                    AddSelectedVisual(entry, instance, material);
                    break;
                }
            case MeshComponentType.Face when entry.Surface is not null:
                {
                    var mesh = new ArrayMesh();
                    if (!TrySetFaceSurface(mesh, entry.Surface, id.Slot))
                    {
                        mesh.Dispose();
                        break;
                    }

                    MeshInstance3D instance = CreateOverlayInstance(
                        "SelectedFace",
                        mesh,
                        _selectedFaceMaterial
                    );
                    AddSelectedVisual(entry, instance, mesh);
                    break;
                }
        }
    }

    // Selected visuals inherit the object's transform, so transform previews need no rebuild.
    // Removing an entry frees them with its root; tolerate an already-freed instance.
    private void AddSelectedVisual(
        VisualEntry entry,
        MeshInstance3D instance,
        IDisposable ownedResource
    )
    {
        instance.Visible = true;
        entry.Root.AddChild(instance);
        _selectedVisuals.Add((instance, ownedResource));
    }

    private void ClearSelectedVisuals()
    {
        foreach ((MeshInstance3D instance, IDisposable owned) in _selectedVisuals)
        {
            if (GodotObject.IsInstanceValid(instance))
            {
                instance.MaterialOverride = null;
                instance.Mesh = null;
                instance.Free();
            }

            owned.Dispose();
        }

        _selectedVisuals.Clear();
    }

    // MeshComponentType ordinals match ComponentMask bit positions (Vertex=0 → 1<<0, …).
    private static bool Includes(ComponentMask mask, MeshComponentType type) =>
        mask.HasFlag((ComponentMask)(1 << (int)type));

    private void SynchronizeHoveredComponent(MeshComponentId? component)
    {
        _hoverVertex.Instance.Visible = false;
        _hoverEdge.Instance.Visible = false;
        _hoverFaceInstance.Visible = false;
        if (
            component is not MeshComponentId id
            || !_entries.TryGetValue(id.ObjectId, out VisualEntry? entry)
        )
        {
            _hoverFaceMesh.ClearSurfaces();
            return;
        }

        switch (id.Type)
        {
            case MeshComponentType.Vertex
                when entry.TryGetVertex(id.Slot, out Vector3 position):
                _hoverVertex.UseMaterial(_stencilReferences.Get(id.ObjectId));
                _hoverVertex.Instance.Transform =
                    entry.Root.Transform * new Transform3D(Basis.Identity, position);
                _hoverVertex.Instance.Visible = true;
                break;
            case MeshComponentType.Edge
                when entry.TryGetEdge(id.Slot, out Vector3 start, out Vector3 end):
                StencilMaterialPair material = _hoverEdge.UseMaterial(
                    _stencilReferences.Get(id.ObjectId)
                );
                ConfigureActiveEdge(_hoverEdge.Instance, material, start, end);
                _hoverEdge.Instance.Transform = entry.Root.Transform;
                _hoverEdge.Instance.Visible = true;
                break;
            case MeshComponentType.Face
                when entry.Surface is not null
                    && TrySetFaceSurface(_hoverFaceMesh, entry.Surface, id.Slot):
                _hoverFaceInstance.Transform = entry.Root.Transform;
                _hoverFaceInstance.Visible = true;
                return;
        }
        _hoverFaceMesh.ClearSurfaces();
    }

    private static void ConfigureActiveEdge(
        MeshInstance3D instance,
        StencilMaterialPair material,
        Vector3 start,
        Vector3 end
    )
    {
        material.SetShaderParameter("edge_start", start);
        material.SetShaderParameter("edge_end", end);

        // The carrier mesh contains only zeroed placeholder vertices; the shader positions its
        // quad from uniforms. Give the renderer the real local-space bounds so frustum culling does
        // not depend on whether the owning object's origin happens to be on screen.
        instance.CustomAabb = new Aabb(start, Vector3.Zero).Expand(end);
        instance.ExtraCullMargin = ActiveEdgeCullMargin;
    }

    private void ClearEntries()
    {
        foreach (VisualEntry entry in _entries.Values)
            entry.Dispose();
        _entries.Clear();
        _dirtyMeshes.Clear();
    }

    private void DisposeResources()
    {
        ClearSelectedVisuals();
        _hoverVertex.Dispose();
        _hoverEdge.Dispose();
        _hoverFaceInstance.MaterialOverride = null;
        _hoverFaceInstance.Mesh = null;
        _root.Free();
        _selectedFaceMaterial.Dispose();
        _hoverFaceMaterial.Dispose();
        _hoverFaceMesh.Dispose();
        _activeEdgeMesh.Dispose();
        _edgeMaterial.Dispose();
        _vertexQuad.Material = null;
        _vertexQuad.Dispose();
        _vertexMaterial.Dispose();
        foreach (Shader shader in _edgeXRayShaders.Values)
            shader.Dispose();
        _edgeXRayShaders.Clear();
        foreach (Shader shader in _vertexXRayShaders.Values)
            shader.Dispose();
        _vertexXRayShaders.Clear();
        _selectedVertexShader.Dispose();
    }

    private static Shader LoadShader(string path) =>
        GD.Load<Shader>(path) ?? throw new InvalidOperationException($"Failed to load '{path}'.");

    private StencilMaterialPair CreateActiveVertexMaterial(
        int stencilReference,
        Color color,
        float sizePixels,
        ComponentRenderPriority renderPriority
    )
    {
        StencilMaterialPair material = CreateActiveMaterial(
            _selectedVertexShader,
            _vertexXRayShaders,
            stencilReference,
            renderPriority
        );
        material.SetShaderParameter("point_color", color);
        material.SetShaderParameter("point_size_pixels", sizePixels);
        return material;
    }

    private StencilMaterialPair CreateActiveEdgeMaterial(
        int stencilReference,
        Color color,
        float widthPixels,
        ComponentRenderPriority renderPriority
    )
    {
        StencilMaterialPair material = CreateActiveMaterial(
            _activeEdgeShader,
            _edgeXRayShaders,
            stencilReference,
            renderPriority
        );
        material.SetShaderParameter("edge_color", color);
        material.SetShaderParameter("line_width_pixels", widthPixels);
        return material;
    }

    private static StencilMaterialPair CreateActiveMaterial(
        Shader source,
        Dictionary<int, Shader> xrayShaders,
        int stencilReference,
        ComponentRenderPriority renderPriority
    )
    {
        Shader? xrayShader = null;
        if (
            stencilReference != 0
            && !xrayShaders.TryGetValue(stencilReference, out xrayShader)
        )
        {
            xrayShader = StencilShader.CreateXRayReader(source, stencilReference);
            xrayShaders.Add(stencilReference, xrayShader);
        }

        return new StencilMaterialPair(source, xrayShader, renderPriority);
    }

    private static ShaderMaterial CreateFaceMaterial(Shader shader, Color color)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("overlay_color", color);
        return material;
    }

    private static MeshInstance3D CreateOverlayInstance(
        string name,
        Mesh mesh,
        Material? material
    ) =>
        new()
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            IgnoreOcclusionCulling = true,
            Visible = false,
        };

    private static void SetSurface(
        ArrayMesh mesh,
        Mesh.PrimitiveType primitive,
        Vector3[] vertices,
        Vector3[]? normals = null,
        Vector2[]? uvs = null
    )
    {
        mesh.ClearSurfaces();
        if (vertices.Length == 0)
            return;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        if (normals is not null)
            arrays[(int)Mesh.ArrayType.Normal] = normals;
        if (uvs is not null)
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        mesh.AddSurfaceFromArrays(primitive, arrays);
    }

    // Faces are overlay-only (no muted batch), so build triangles for one slot on demand.
    private static bool TrySetFaceSurface(ArrayMesh mesh, MeshSurface surface, int faceSlot)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        for (int triangle = 0; triangle < surface.FaceSlots.Count; triangle++)
        {
            if (surface.FaceSlots[triangle] != faceSlot)
                continue;

            int element = triangle * 3;
            for (int corner = 0; corner < 3; corner++)
            {
                int index = surface.ElementIndices[element + corner];
                vertices.Add(ToGodot(surface.Vertices[index]));
                normals.Add(ToGodot(surface.Normals[index]));
            }
        }

        if (vertices.Count == 0)
        {
            mesh.ClearSurfaces();
            return false;
        }

        SetSurface(mesh, Mesh.PrimitiveType.Triangles, vertices.ToArray(), normals: normals.ToArray());
        return true;
    }

    private sealed class StencilMaterialPair : IDisposable
    {
        private readonly ShaderMaterial? _xrayMaterial;

        public StencilMaterialPair(
            Shader normalShader,
            Shader? xrayShader,
            ComponentRenderPriority renderPriority
        )
        {
            int priority = (int)renderPriority;
            Material = new ShaderMaterial
            {
                Shader = normalShader,
                RenderPriority = priority,
            };
            if (xrayShader is null)
                return;

            _xrayMaterial = new ShaderMaterial
            {
                Shader = xrayShader,
                RenderPriority = priority,
            };
            Material.NextPass = _xrayMaterial;
        }

        public ShaderMaterial Material { get; }

        public void SetShaderParameter(StringName name, Variant value)
        {
            Material.SetShaderParameter(name, value);
            _xrayMaterial?.SetShaderParameter(name, value);
        }

        public void Dispose()
        {
            Material.NextPass = null;
            _xrayMaterial?.Dispose();
            Material.Dispose();
        }
    }

    // Hover materials are keyed by stencil reference, not owner ID: an object can be re-acquired
    // with a different reference under the same ID (undo of a removal, or a new document reusing
    // IDs), and the X-ray pass must read the current reference.
    private sealed class HoverOverlay(
        MeshInstance3D instance,
        Func<int, StencilMaterialPair> createMaterial
    ) : IDisposable
    {
        private StencilMaterialPair? _material;
        private int? _stencilReference;

        public MeshInstance3D Instance { get; } = instance;

        public StencilMaterialPair UseMaterial(int stencilReference)
        {
            if (_material is not null && _stencilReference == stencilReference)
                return _material;

            Instance.MaterialOverride = null;
            _material?.Dispose();
            _material = createMaterial(stencilReference);
            _stencilReference = stencilReference;
            Instance.MaterialOverride = _material.Material;
            return _material;
        }

        public void Dispose()
        {
            Instance.MaterialOverride = null;
            Instance.Mesh = null;
            _material?.Dispose();
        }
    }

    private static ArrayMesh CreateActiveEdgeMesh()
    {
        var mesh = new ArrayMesh();
        SetSurface(
            mesh,
            Mesh.PrimitiveType.Triangles,
            new Vector3[6],
            uvs:
            [
                new Vector2(0.0f, -1.0f),
                new Vector2(1.0f, -1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, -1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f),
            ]
        );
        return mesh;
    }

    private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);

    private sealed class VisualEntry : IDisposable
    {
        private readonly MultiMesh _vertices = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
        };
        private readonly ArrayMesh _edges = new();
        private readonly MultiMeshInstance3D _vertexInstance;
        private readonly MeshInstance3D _edgeInstance;
        private bool _disposed;

        public VisualEntry(
            MeshObject meshObject,
            Node3D parent,
            Transform3D transform,
            QuadMesh vertexQuad,
            Material edgeMaterial
        )
        {
            Root = new Node3D
            {
                Name = $"MeshObject_{meshObject.Id.Value}",
                Transform = transform,
            };
            _vertices.Mesh = vertexQuad;
            _vertexInstance = new MultiMeshInstance3D
            {
                Name = "Vertices",
                Multimesh = _vertices,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _edgeInstance = new MeshInstance3D
            {
                Name = "Edges",
                Mesh = _edges,
                MaterialOverride = edgeMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            Root.AddChild(_vertexInstance);
            Root.AddChild(_edgeInstance);
            parent.AddChild(Root);
        }

        public Node3D Root { get; }

        public SpatialMesh? SpatialMesh { get; private set; }

        public MeshSurface? Surface { get; private set; }

        public void UpdateGeometry(GDMesh mesh)
        {
            SpatialMesh = mesh.SpatialMesh;
            Surface = mesh.Surface;
            SpatialMesh.WithVertices((slots, _, positions) =>
            {
                _vertices.InstanceCount = 0;
                _vertices.InstanceCount = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                {
                    _vertices.SetInstanceTransform(
                        i,
                        new Transform3D(Basis.Identity, ToGodot(positions[i]))
                    );
                }
            });
            SetSurface(_edges, Mesh.PrimitiveType.Lines, PreviewGeometryRenderer.CreateWireVertices(SpatialMesh));
        }

        public bool TryGetVertex(int slot, out Vector3 position)
        {
            position = default;
            if (SpatialMesh is not { } mesh || !mesh.IsVertexAlive(slot))
                return false;
            position = ToGodot(mesh.GetVertexData(slot).position);
            return true;
        }

        // Component edge slots are the lower twin handle; reject the upper twin and dead slots.
        public bool TryGetEdge(int slot, out Vector3 start, out Vector3 end)
        {
            start = default;
            end = default;
            if (SpatialMesh is not { } mesh || !mesh.IsHalfEdgeAlive(slot))
                return false;
            HalfEdge halfEdge = mesh.GetHalfEdgeData(slot).topology;
            if (slot > halfEdge.TwinHalfEdge)
                return false;
            start = ToGodot(mesh.GetVertexData(halfEdge.SourceVertex).position);
            end = ToGodot(
                mesh.GetVertexData(mesh.GetHalfEdgeData(halfEdge.TwinHalfEdge).topology.SourceVertex)
                    .position
            );
            return true;
        }

        public void SetVisibleComponents(ComponentMask mask)
        {
            _vertexInstance.Visible = mask.HasFlag(ComponentMask.Vertex);
            _edgeInstance.Visible = mask.HasFlag(ComponentMask.Edge);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            SpatialMesh = null;
            Surface = null;
            _edgeInstance.MaterialOverride = null;
            _edgeInstance.Mesh = null;
            _vertexInstance.Multimesh = null;
            Root.Free();
            _edges.Dispose();
            _vertices.Dispose();
        }
    }
}
