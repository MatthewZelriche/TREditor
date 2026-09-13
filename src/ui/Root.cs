#nullable enable

using System;
using Godot;

public partial class Root : Node
{
    private static readonly Vector2I MinimumWindowSize = new(640, 360);
    private const string SceneFilter = "*.res;TREditor Scene";

    private OsLogger? _osLogger;
    private Application? _application;
    private KeybindingManager? _keybindings;
    private AppMenuBar? _menuBar;
    private KeybindingsDialog? _keybindingsDialog;
    private LogWindow? _logWindow;
    private FileDialog? _openSceneDialog;
    private FileDialog? _saveSceneDialog;
    private AcceptDialog? _fileErrorDialog;
    private AcceptDialog? _fatalErrorDialog;
    private bool _fatalShutdownRequested;

    internal Application? CurrentApplication => _application;

    internal KeybindingManager? CurrentKeybindings => _keybindings;

    internal OsLogger? CurrentLogger => _osLogger;

    public override void _EnterTree()
    {
        _osLogger = new OsLogger();
        OS.AddLogger(_osLogger);
        _keybindings = new KeybindingManager();
        _keybindings.Load();
    }

    public override void _Ready()
    {
        GetWindow().MinSize = MinimumWindowSize;
        _menuBar = GetNode<AppMenuBar>("UI/AppMenuBar");
        _keybindingsDialog = GetNode<KeybindingsDialog>("%KeybindingsDialog");
        _logWindow = GetNode<LogWindow>("%LogWindow");
        if (_osLogger is not null)
            _logWindow.Bind(_osLogger);
        _logWindow.FatalErrorLogged += OnFatalLog;

        _application = new Application(GetNode<Node3D>("%MapRoot"));
        _openSceneDialog = GetNode<FileDialog>("%OpenSceneDialog");
        _saveSceneDialog = GetNode<FileDialog>("%SaveSceneDialog");
        _fileErrorDialog = GetNode<AcceptDialog>("%FileErrorDialog");
        _fatalErrorDialog = GetNode<AcceptDialog>("%FatalErrorDialog");

        GetNode<SelectionModeToolbar>("UI/ToolbarStrip/ToolbarMargin/SelectionModes")
            .Bind(_application.SelectionMode);
        GetNode<ToolsToolbar>("UI/Toolbar/ToolbarMargin/Tools")
            .Bind(_application.SelectionMode);
        GetNode<MainViewportArea>("UI/MainViewportArea").Bind(_application);
        _application.OperationFailed += OnOperationFailed;
        _application.FatalError += OnFatalError;
        if (_application.FatalFailure is { } fatalFailure)
            OnFatalError(fatalFailure.Operation, fatalFailure.Exception);

        ConfigureFileDialog(_openSceneDialog, FileDialog.FileModeEnum.OpenFile);
        ConfigureFileDialog(_saveSceneDialog, FileDialog.FileModeEnum.SaveFile);

        _menuBar.MenuActionRequested += OnMenuActionRequested;
        _openSceneDialog.FileSelected += OnOpenSceneSelected;
        _saveSceneDialog.FileSelected += OnSaveSceneSelected;
        _fatalErrorDialog.Confirmed += CloseAfterFatalError;
        _fatalErrorDialog.Canceled += CloseAfterFatalError;
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

        if (_fatalErrorDialog is not null)
        {
            _fatalErrorDialog.Confirmed -= CloseAfterFatalError;
            _fatalErrorDialog.Canceled -= CloseAfterFatalError;
        }

        if (_logWindow is not null)
            _logWindow.FatalErrorLogged -= OnFatalLog;

        if (_application is not null)
        {
            _application.OperationFailed -= OnOperationFailed;
            _application.FatalError -= OnFatalError;
        }

        _application?.Dispose();
        _keybindings?.Dispose();
        if (_osLogger is not null)
        {
            OS.RemoveLogger(_osLogger);
            _osLogger.Dispose();
        }

        _osLogger = null;
        _application = null;
        _keybindings = null;
        _menuBar = null;
        _keybindingsDialog = null;
        _logWindow = null;
        _openSceneDialog = null;
        _saveSceneDialog = null;
        _fileErrorDialog = null;
        _fatalErrorDialog = null;
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
            case AppMenuBar.AppMenuAction.Logs:
                _logWindow?.ShowDialog();
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
            PresentOperationError(operation, exception);
        }
    }

    private void OnOperationFailed(string operation, Exception exception) =>
        PresentOperationError(operation, exception);

    private void OnFatalError(string operation, Exception exception)
    {
        string details = $"Fatal scene service error while {operation}: {exception}";
        _osLogger?.LogFatal(details);
        GD.PrintErr($"FATAL: {details}");
        ShowFatalErrorDialog(
            "A fatal error occurred while "
                + operation
                + ". The application cannot continue safely.\n\n"
                + "Saving after a fatal error is not supported yet.\n\n"
                + "Debug details:\n"
                + exception
        );
    }

    private void OnFatalLog(string message)
    {
        ShowFatalErrorDialog(
            "An unexpected fatal error occurred. The application cannot continue safely.\n\n"
                + "Debug details:\n"
                + message
        );
    }

    private void ShowFatalErrorDialog(string message)
    {
        if (_fatalShutdownRequested)
            return;

        _fatalShutdownRequested = true;

        if (_fatalErrorDialog is null)
        {
            GetTree().Quit();
            return;
        }

        _fatalErrorDialog.Title = "Application crashed";
        _fatalErrorDialog.DialogText = message;
        _fatalErrorDialog.GetOkButton().Text = "Close Application";
        _fatalErrorDialog.PopupCentered();
    }

    private void CloseAfterFatalError()
    {
        // Future recovery point: offer to save a recovery copy here only after the document/service
        // boundaries guarantee that the authoritative document is stable and safe to serialize.
        // The recovery path must serialize the document directly, never derived service state.
        GetTree().Quit();
    }

    private void PresentOperationError(string operation, Exception exception)
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
