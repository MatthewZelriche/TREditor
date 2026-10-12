using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed record FaceExtrusionPlan(MeshObject Object, int[] Faces);

internal sealed class ExtrudeFacesCommand : MeshPatchCommandBase
{
    private const string StaleSelection =
        "The selected faces are missing or stale and cannot be extruded.";

    private readonly int[] _faces;
    private readonly NumericsVector3 _offset;
    private int[] _capFaces = [];

    private ExtrudeFacesCommand(
        FaceExtrusionPlan plan,
        NumericsVector3 offset,
        IEditorSelection selection
    )
        : base(selection)
    {
        Object = plan.Object;
        _faces = plan.Faces;
        _offset = offset;
    }

    public override string DisplayName => "Extrude Faces";

    protected override MeshObject Object { get; }

    protected override string StaleMessage => StaleSelection;

    protected override MeshPatch RecordEdit() =>
        Object.Mesh.SpatialMesh.Record(mesh =>
        {
            FaceExtrusion extrusion = mesh.ExtrudeFaces(_faces, _offset);
            _capFaces = extrusion.Faces;
        });

    protected override SelectionValue SelectionAfterEdit() =>
        SelectionForComponents(MeshComponentType.Face, _capFaces);

    internal static EditorToolAction CreateAction(IEditorToolContext context, float length)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ActionOrReason(() =>
        {
            if (!float.IsFinite(length) || length < LengthToolBase.MinimumLength)
                throw new ArgumentException("Extrude length must be a positive finite value.");

            FaceExtrusionPlan plan = ResolvePlan(context.Document, context.Selection.Current);
            return Create(plan, CombinedWorldNormal(plan) * length, context.Selection);
        });
    }

    internal static FaceExtrusionPlan ResolvePlan(
        IEditorDocumentReader document,
        SelectionValue selection
    )
    {
        MeshObject meshObject = RequireHomogeneousComponents(
            document,
            selection,
            MeshComponentType.Face,
            StaleSelection,
            "Deselect all objects; Extrude Faces only accepts face selections.",
            "Select at least one face from one mesh.",
            "Only faces can be extruded; deselect all selected vertices and edges.",
            out IReadOnlyList<MeshComponentId> components
        );

        var faces = new int[components.Count];
        for (int index = 0; index < faces.Length; index++)
        {
            int face = components[index].Slot;
            if (!meshObject.Mesh.SpatialMesh.IsFaceAlive(face))
                throw new ArgumentException(StaleSelection);
            faces[index] = face;
        }
        RequireInvertibleTransform(meshObject.Transform);
        return new FaceExtrusionPlan(meshObject, faces);
    }

    internal static ExtrudeFacesCommand Create(
        FaceExtrusionPlan plan,
        Vector3 worldOffset,
        IEditorSelection selection
    )
    {
        Vector3 localOffset = plan.Object.Transform.Basis.Inverse() * worldOffset;
        if (!localOffset.IsFinite())
            throw new ArgumentException("Extruding the selected faces produced an invalid offset.");
        return new ExtrudeFacesCommand(
            plan,
            new NumericsVector3(localOffset.X, localOffset.Y, localOffset.Z),
            selection
        );
    }

    private static Vector3 CombinedWorldNormal(FaceExtrusionPlan plan)
    {
        Transform3D transform = plan.Object.Transform;
        float determinant = RequireInvertibleTransform(transform);
        Basis normalTransform = transform.Basis.Inverse().Transposed();
        float orientation = determinant < 0.0f ? -1.0f : 1.0f;
        Vector3 combined = Vector3.Zero;
        foreach (int face in plan.Faces)
        {
            NumericsVector3 normal = plan.Object.Mesh.SpatialMesh.GetFaceData(face).normal;
            Vector3 world = orientation * (normalTransform * ToGodot(normal));
            if (world.IsZeroApprox() || !world.IsFinite())
                throw new ArgumentException("A selected face is degenerate.");
            combined += world.Normalized();
        }

        if (combined.IsZeroApprox() || !combined.IsFinite())
            throw new ArgumentException("The selected faces have no combined extrusion direction.");
        return combined.Normalized();
    }

    private static float RequireInvertibleTransform(Transform3D transform)
    {
        float determinant = transform.Basis.Determinant();
        if (!float.IsFinite(determinant) || Mathf.IsZeroApprox(determinant))
            throw new ArgumentException("The mesh object's transform must be invertible.");
        return determinant;
    }

    private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);
}
