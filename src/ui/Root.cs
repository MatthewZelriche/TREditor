#nullable enable

using System;
using Godot;

public partial class Root : Node
{
    private static readonly Vector2I MinimumWindowSize = new(640, 360);
    private const string SceneFilter = "*.res;TREditor Scene";

    private Application? _application;
    private KeybindingManager? _keybindings;
    private AppMenuBar? _menuBar;
    private KeybindingsDialog? _keybindingsDialog;
    private FileDialog? _openSceneDialog;
    private FileDialog? _saveSceneDialog;
    private AcceptDialog? _fileErrorDialog;

    internal Application? CurrentApplication => _application;

    internal KeybindingManager? CurrentKeybindings => _keybindings;

    public override void _EnterTree()
    {
        _keybindings = new KeybindingManager();
        _keybindings.Load();
    }

    public override void _Ready()
    {
        GetWindow().MinSize = MinimumWindowSize;
        _application = new Application(GetNode<Node3D>("%MapRoot"));
        _menuBar = GetNode<AppMenuBar>("UI/AppMenuBar");
        _keybindingsDialog = GetNode<KeybindingsDialog>("%KeybindingsDialog");
        _openSceneDialog = GetNode<FileDialog>("%OpenSceneDialog");
        _saveSceneDialog = GetNode<FileDialog>("%SaveSceneDialog");
        _fileErrorDialog = GetNode<AcceptDialog>("%FileErrorDialog");

        GetNode<SelectionModeToolbar>("UI/ToolbarStrip/ToolbarMargin/SelectionModes")
            .Bind(_application.SelectionMode);
        GetNode<ToolsToolbar>("UI/Toolbar/ToolbarMargin/Tools")
            .Bind(_application.SelectionMode);

        ConfigureFileDialog(_openSceneDialog, FileDialog.FileModeEnum.OpenFile);
        ConfigureFileDialog(_saveSceneDialog, FileDialog.FileModeEnum.SaveFile);

        _menuBar.MenuActionRequested += OnMenuActionRequested;
        _openSceneDialog.FileSelected += OnOpenSceneSelected;
        _saveSceneDialog.FileSelected += OnSaveSceneSelected;
    }

    public override void _Process(double delta)
    {
        _application?.Update(delta);
    }

    public override void _ExitTree()
    {
        if (_menuBar is not null)
            _menuBar.MenuActionRequested -= OnMenuActionRequested;

        if (_openSceneDialog is not null)
            _openSceneDialog.FileSelected -= OnOpenSceneSelected;

        if (_saveSceneDialog is not null)
            _saveSceneDialog.FileSelected -= OnSaveSceneSelected;

        _application?.Dispose();
        _keybindings?.Dispose();
        _application = null;
        _keybindings = null;
        _menuBar = null;
        _keybindingsDialog = null;
        _openSceneDialog = null;
        _saveSceneDialog = null;
        _fileErrorDialog = null;
    }

    private static void ConfigureFileDialog(
        FileDialog dialog,
        FileDialog.FileModeEnum fileMode
    )
    {
        dialog.Access = FileDialog.AccessEnum.Filesystem;
        dialog.FileMode = fileMode;
        dialog.UseNativeDialog = true;
        dialog.Filters = [SceneFilter];
    }

    private void OnMenuActionRequested(int actionId)
    {
        if (_application is null)
            return;

        switch ((AppMenuBar.AppMenuAction)actionId)
        {
            case AppMenuBar.AppMenuAction.NewDocument:
                ExecuteFileAction("New scene", _application.NewScene);
                break;
            case AppMenuBar.AppMenuAction.OpenDocument:
                _openSceneDialog?.PopupFileDialog();
                break;
            case AppMenuBar.AppMenuAction.SaveDocument:
                if (_application.CurrentFilePath is null)
                    ShowSaveSceneDialog();
                else
                    ExecuteFileAction("Save scene", _application.SaveScene);
                break;
            case AppMenuBar.AppMenuAction.SaveDocumentAs:
                ShowSaveSceneDialog();
                break;
            case AppMenuBar.AppMenuAction.Quit:
                GetTree().Quit();
                break;
            case AppMenuBar.AppMenuAction.Keybindings:
                if (_keybindings is not null)
                    _keybindingsDialog?.ShowDialog(_keybindings);
                break;
        }
    }

    private void ShowSaveSceneDialog()
    {
        if (_saveSceneDialog is null)
            return;

        if (_application?.CurrentFilePath is string currentPath)
            _saveSceneDialog.CurrentPath = currentPath;
        else
            _saveSceneDialog.CurrentFile = "Untitled.res";

        _saveSceneDialog.PopupFileDialog();
    }

    private void OnOpenSceneSelected(string path)
    {
        if (_application is not null)
            ExecuteFileAction("Open scene", () => _application.OpenScene(path));
    }

    private void OnSaveSceneSelected(string path)
    {
        if (_application is not null)
            ExecuteFileAction("Save scene", () => _application.SaveSceneAs(path));
    }

    private void ExecuteFileAction(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            GD.PushError($"{operation} failed: {exception}");

            if (_fileErrorDialog is not null)
            {
                _fileErrorDialog.Title = $"{operation} failed";
                _fileErrorDialog.DialogText = exception.Message;
                _fileErrorDialog.PopupCentered();
            }
        }
    }
}
