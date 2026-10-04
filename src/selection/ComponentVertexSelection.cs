using System;
using System.Collections.Generic;
using System.Linq;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

// A validated expansion of selected vertices, edges, and faces into the unique mesh vertices that
// component tools actually edit.
internal sealed record ComponentVertexSelection(
    MeshObject Object,
    int[] Vertices,
    NumericsVector3[] Positions
);

internal static class ComponentVertexSelectionResolver
{
    public static bool TryResolve(
        IEditorDocumentReader document,
        IReadOnlySet<MeshComponentId> components,
        ComponentMask mask,
        PreviewState? previews,
        out ComponentVertexSelection[] selections
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(components);
        var byObject = new Dictionary<MeshObjectId, Accumulator>();

        foreach (MeshComponentId component in components)
        {
            if (!mask.Includes(component.Type))
                continue;

            if (!document.TryGetObject(component.ObjectId, out MeshObject meshObject))
                return Fail(out selections);

            GDMesh mesh = previews is not null
                ? previews.MeshFor(meshObject)
                : meshObject.Mesh;
            if (!byObject.TryGetValue(component.ObjectId, out Accumulator? accumulator))
            {
                accumulator = new Accumulator(meshObject, mesh);
                byObject.Add(component.ObjectId, accumulator);
            }
            else if (!ReferenceEquals(accumulator.Mesh, mesh))
            {
                return Fail(out selections);
            }

            if (!AddComponentVertices(mesh.SpatialMesh, component, accumulator.Vertices))
                return Fail(out selections);
        }

        if (byObject.Count == 0)
            return Fail(out selections);

        selections = new ComponentVertexSelection[byObject.Count];
        int selectionIndex = 0;
        foreach (Accumulator accumulator in byObject.Values)
        {
            int[] vertices = accumulator.Vertices.Order().ToArray();
            var positions = new NumericsVector3[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
                positions[index] = accumulator.Mesh.SpatialMesh.GetVertexData(vertices[index]).position;

            selections[selectionIndex++] = new ComponentVertexSelection(
                accumulator.Object,
                vertices,
                positions
            );
        }

        return true;
    }

    private static bool AddComponentVertices(
        SpatialMesh mesh,
        MeshComponentId component,
        HashSet<int> vertices
    )
    {
        switch (component.Type)
        {
            case MeshComponentType.Vertex:
                if (!mesh.IsVertexAlive(component.Slot))
                    return false;
                vertices.Add(component.Slot);
                return true;

            case MeshComponentType.Edge:
                if (!mesh.IsHalfEdgeAlive(component.Slot))
                    return false;
                HalfEdge edge = mesh.GetHalfEdgeData(component.Slot).topology;
                if (component.Slot > edge.TwinHalfEdge || !mesh.IsHalfEdgeAlive(edge.TwinHalfEdge))
                    return false;
                vertices.Add(edge.SourceVertex);
                vertices.Add(mesh.GetHalfEdgeData(edge.TwinHalfEdge).topology.SourceVertex);
                return true;

            case MeshComponentType.Face:
                if (!mesh.IsFaceAlive(component.Slot))
                    return false;
                int start = mesh.GetFaceData(component.Slot).topology.AdjacentHalfEdge;
                int halfEdge = start;
                for (int count = 0; count <= mesh.HalfEdgeCount; count++)
                {
                    if (!mesh.IsHalfEdgeAlive(halfEdge))
                        return false;
                    HalfEdge corner = mesh.GetHalfEdgeData(halfEdge).topology;
                    if (corner.AdjacentFace != component.Slot || !mesh.IsVertexAlive(corner.SourceVertex))
                        return false;
                    vertices.Add(corner.SourceVertex);
                    halfEdge = corner.NextHalfEdge;
                    if (halfEdge == start)
                        return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool Fail(out ComponentVertexSelection[] selections)
    {
        selections = [];
        return false;
    }

    private sealed record Accumulator(MeshObject Object, GDMesh Mesh)
    {
        public HashSet<int> Vertices { get; } = [];
    }
}
