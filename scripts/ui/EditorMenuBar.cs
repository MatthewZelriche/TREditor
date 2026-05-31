using Godot;

public partial class EditorMenuBar : MenuBar
{
    private const int UndoMenuId = 0;
    private const int RedoMenuId = 1;

    private EditorSession _session;
    private PopupMenu _editMenu;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _editMenu = GetNodeOrNull<PopupMenu>("Edit");

        if (_session == null)
        {
            GD.PushWarning("EditorMenuBar could not find EditorSession.");
            return;
        }

        if (_editMenu == null)
        {
            GD.PushWarning("EditorMenuBar could not find the Edit menu.");
            return;
        }

        _editMenu.IdPressed += OnEditMenuIdPressed;
        _editMenu.AboutToPopup += UpdateEditMenuState;
        _session.Commands.CommandHistoryChanged += UpdateEditMenuState;
        UpdateEditMenuState();
    }

    private void OnEditMenuIdPressed(long id)
    {
        switch (id)
        {
            case UndoMenuId:
                _session.Commands.Undo();
                break;
            case RedoMenuId:
                _session.Commands.Redo();
                break;
        }
    }

    private void UpdateEditMenuState()
    {
        SetMenuItemDisabled(UndoMenuId, !_session.Commands.CanUndo);
        SetMenuItemDisabled(RedoMenuId, !_session.Commands.CanRedo);
    }

    private void SetMenuItemDisabled(int id, bool disabled)
    {
        int index = _editMenu.GetItemIndex(id);
        if (index >= 0)
        {
            _editMenu.SetItemDisabled(index, disabled);
        }
    }
}
