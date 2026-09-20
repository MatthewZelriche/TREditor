using System;

public interface ISceneService : IDisposable
{
    /// <summary>
    /// Replaces the document used by this service and clears state derived from the previous document.
    /// Existing objects in <paramref name="document"/> are replayed separately by
    /// <see cref="SceneServices"/>. The service does not own or dispose either document.
    /// </summary>
    /// <remarks>
    /// After successful construction, <see cref="SceneServices"/> catches service failures at its
    /// boundary, reports a fatal error, and stops dispatching further work. Implementations are not
    /// required to support recovery.
    /// As a result, this means throwing exceptions should be reserved for fatal scenarios only.
    /// </remarks>
    void SetDocument(Document document);

    // Handles a committed document change. Query the document by ID for current records; removed
    // objects are already gone from the document when this runs.
    void OnDocumentChanged(DocumentChangeSet changeSet);

    // Preview-only transform notification. Committed transforms arrive through OnDocumentChanged.
    void OnPreviewTransformChanged(MeshObjectId id);

    // Preview-only geometry used by tools before an object belongs to the document.
    void OnPreviewGeometryChanged() { }

    void Flush();
}
