// The object entry in a committed DocumentChangeSet. Carries identity and kind only — never live
// model objects or collections.
public readonly record struct DocumentObjectChange(MeshObjectId ObjectId, DocumentChangeKind Kind);
