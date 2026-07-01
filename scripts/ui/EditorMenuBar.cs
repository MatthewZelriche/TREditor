using Godot;
using Godot.Collections;

public partial class EditorMenuBar : MenuBar
{
    private const int UndoMenuId = 0;
    private const int RedoMenuId = 1;

    private const int FileNewId = 0;
    private const int FileOpenId = 1;
    private const int FileSaveId = 2;
    private const int FileQuitId = 3;

    private const int KeybindingsMenuId = 1;
    private const int ThirdPartyLicensesMenuId = 0;

    private EditorSession _session;
    private PopupMenu _editMenu;
    private PopupMenu _fileMenu;
    private PopupMenu _settingsMenu;
    private PopupMenu _aboutMenu;
    private FileDialog _fileDialog;
    private LicensesDialog _licensesDialog;
    private KeybindingsDialog _keybindingsDialog;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _editMenu = GetNodeOrNull<PopupMenu>("Edit");
        _fileMenu = GetNodeOrNull<PopupMenu>("File");
        _settingsMenu = GetNodeOrNull<PopupMenu>("Settings");

        if (_session == null)
        {
            GD.PushWarning("EditorMenuBar could not find EditorSession.");
            return;
        }

        WireEditMenu();
        WireFileMenu();
        WireSettingsMenu();
        WireAboutMenu();
        WireKeybindingShortcuts();
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

    private void WireSettingsMenu()
    {
        if (_settingsMenu == null)
        {
            GD.PushWarning("EditorMenuBar could not find the Settings menu.");
            return;
        }

        _settingsMenu.IdPressed += OnSettingsMenuIdPressed;
    }

    private void WireKeybindingShortcuts()
    {
        if (KeybindingService.Instance == null)
        {
            GD.PushWarning("EditorMenuBar could not find the KeybindingService.");
            return;
        }

        KeybindingService.Instance.BindingChanged += OnBindingChanged;
        RefreshMenuShortcuts();
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

    private void OnSettingsMenuIdPressed(long id)
    {
        if (id == KeybindingsMenuId)
            ShowKeybindingsDialog();
    }

    private void ShowKeybindingsDialog()
    {
        if (_keybindingsDialog == null)
        {
            PackedScene scene = GD.Load<PackedScene>("res://scripts/ui/KeybindingsDialog.tscn");
            _keybindingsDialog = scene.Instantiate<KeybindingsDialog>();
            AddChild(_keybindingsDialog);
        }

        _keybindingsDialog.PopupCentered();
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

    private void OnBindingChanged(string actionId)
    {
        if (
            actionId
            is KeybindingActions.FileNew
                or KeybindingActions.FileOpen
                or KeybindingActions.FileSave
                or KeybindingActions.FileQuit
                or KeybindingActions.EditUndo
                or KeybindingActions.EditRedo
        )
        {
            RefreshMenuShortcuts();
        }
    }

    private void RefreshMenuShortcuts()
    {
        SetMenuShortcut(_fileMenu, FileNewId, KeybindingActions.FileNew);
        SetMenuShortcut(_fileMenu, FileOpenId, KeybindingActions.FileOpen);
        SetMenuShortcut(_fileMenu, FileSaveId, KeybindingActions.FileSave);
        SetMenuShortcut(_fileMenu, FileQuitId, KeybindingActions.FileQuit);
        SetMenuShortcut(_editMenu, UndoMenuId, KeybindingActions.EditUndo);
        SetMenuShortcut(_editMenu, RedoMenuId, KeybindingActions.EditRedo);
    }

    private static void SetMenuShortcut(PopupMenu menu, int itemId, string actionId)
    {
        if (menu == null || KeybindingService.Instance == null)
            return;

        int index = menu.GetItemIndex(itemId);
        if (index < 0)
            return;

        Shortcut shortcut = new();
        InputBinding binding = KeybindingService.Instance.GetBinding(actionId);
        if (binding != null)
        {
            Array events = new();
            foreach (InputEvent input in binding.ToInputEvents())
                events.Add(input);
            shortcut.Events = events;
        }

        menu.SetItemShortcut(index, shortcut, true);
    }

    public override void _ExitTree()
    {
        if (_editMenu != null)
        {
            _editMenu.IdPressed -= OnEditMenuIdPressed;
            _editMenu.AboutToPopup -= UpdateEditMenuState;
        }
        if (_fileMenu != null)
            _fileMenu.IdPressed -= OnFileMenuIdPressed;
        if (_settingsMenu != null)
            _settingsMenu.IdPressed -= OnSettingsMenuIdPressed;
        if (_aboutMenu != null)
            _aboutMenu.IdPressed -= OnAboutMenuIdPressed;
        if (_session?.Commands != null)
            _session.Commands.CommandHistoryChanged -= UpdateEditMenuState;
        if (KeybindingService.Instance != null)
            KeybindingService.Instance.BindingChanged -= OnBindingChanged;
    }
}
