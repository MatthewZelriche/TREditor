using Godot;

public partial class EditorMenuBar : MenuBar
{
    private const int UndoMenuId = 0;
    private const int RedoMenuId = 1;

    private const int FileNewId = 0;
    private const int FileOpenId = 1;
    private const int FileSaveId = 2;
    private const int FileQuitId = 3;

    private const int ThirdPartyLicensesMenuId = 0;

    private EditorSession _session;
    private PopupMenu _editMenu;
    private PopupMenu _fileMenu;
    private PopupMenu _aboutMenu;
    private FileDialog _fileDialog;
    private LicensesDialog _licensesDialog;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _editMenu = GetNodeOrNull<PopupMenu>("Edit");
        _fileMenu = GetNodeOrNull<PopupMenu>("File");

        if (_session == null)
        {
            GD.PushWarning("EditorMenuBar could not find EditorSession.");
            return;
        }

        WireEditMenu();
        WireFileMenu();
        WireAboutMenu();
    }

    private void WireEditMenu()
    {
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

    private void WireFileMenu()
    {
        if (_fileMenu == null)
        {
            GD.PushWarning("EditorMenuBar could not find the File menu.");
            return;
        }

        _fileMenu.IdPressed += OnFileMenuIdPressed;
    }

    private void WireAboutMenu()
    {
        _aboutMenu = GetNodeOrNull<PopupMenu>("About");
        if (_aboutMenu == null)
        {
            GD.PushWarning("EditorMenuBar could not find the About menu.");
            return;
        }

        _aboutMenu.IdPressed += OnAboutMenuIdPressed;
    }

    private void OnAboutMenuIdPressed(long id)
    {
        if (id == ThirdPartyLicensesMenuId)
            ShowLicensesDialog();
    }

    private void ShowLicensesDialog()
    {
        if (_licensesDialog == null)
        {
            PackedScene scene = GD.Load<PackedScene>("res://scripts/ui/LicensesDialog.tscn");
            _licensesDialog = scene.Instantiate<LicensesDialog>();
            AddChild(_licensesDialog);
        }

        _licensesDialog.PopupCentered();
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

    private void OnFileMenuIdPressed(long id)
    {
        switch (id)
        {
            case FileNewId:
                _session.Document.New();
                break;
            case FileOpenId:
                ShowFileDialog(FileDialog.FileModeEnum.OpenFile, "Open Document");
                break;
            case FileSaveId:
                ShowFileDialog(FileDialog.FileModeEnum.SaveFile, "Save Document");
                break;
            case FileQuitId:
                GetTree().Quit();
                break;
        }
    }

    private void ShowFileDialog(FileDialog.FileModeEnum mode, string title)
    {
        EnsureFileDialog();
        _fileDialog.FileMode = mode;
        _fileDialog.Title = title;
        _fileDialog.PopupCentered(new Vector2I(800, 600));
    }

    private void EnsureFileDialog()
    {
        if (_fileDialog != null)
        {
            return;
        }

        _fileDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
        };
        _fileDialog.AddFilter($"*.{EditorDocumentSerializer.FileExtension}", "TREditor Document");
        _fileDialog.FileSelected += OnFileDialogFileSelected;
        AddChild(_fileDialog);
    }

    private void OnFileDialogFileSelected(string path)
    {
        try
        {
            switch (_fileDialog.FileMode)
            {
                case FileDialog.FileModeEnum.OpenFile:
                    _session.Document.Open(path);
                    break;
                case FileDialog.FileModeEnum.SaveFile:
                    _session.Document.Save(path);
                    break;
            }
        }
        catch (System.Exception exception)
        {
            GD.PushError($"Document operation failed for '{path}': {exception.Message}");
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
