using System;

// Application-owned view handed to tools. Picking, selection, and preview state survive scene
// replacement, so they are held directly; the document is resolved per call because it changes
// identity on New and Open. This context borrows its collaborators and never disposes them.
public sealed class EditorToolContext : IEditorToolContext
{
    private readonly Func<IEditorDocumentReader?> _documentAccessor;

    public EditorToolContext(
        Func<IEditorDocumentReader?> documentAccessor,
        IEditorCommandSink commands,
        PickingService picking,
        SelectionState selection,
        PreviewState preview
    )
    {
        ArgumentNullException.ThrowIfNull(documentAccessor);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(picking);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(preview);

        _documentAccessor = documentAccessor;
        Commands = commands;
        Picking = picking;
        Selection = selection;
        Preview = preview;
    }

    public IEditorDocumentReader Document =>
        _documentAccessor()
        ?? throw new InvalidOperationException("There is no document available to tools.");

    public IEditorCommandSink Commands { get; }

    public PickingService Picking { get; }

    public SelectionState Selection { get; }

    public PreviewState Preview { get; }
}
