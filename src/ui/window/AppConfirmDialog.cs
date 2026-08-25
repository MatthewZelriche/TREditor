#nullable enable

using Godot;

public partial class AppConfirmDialog : Window
{
    private string _dialogText = string.Empty;
    private string _confirmButtonText = "OK";
    private string _cancelButtonText = "Cancel";

    private Label? _messageLabel;
    private Button? _confirmButton;
    private Button? _cancelButton;

    [Signal]
    public delegate void ConfirmedEventHandler();

    [Signal]
    public delegate void CanceledEventHandler();

    [Export(PropertyHint.MultilineText)]
    public string DialogText
    {
        get => _dialogText;
        set
        {
            _dialogText = value;
            RefreshContents();
        }
    }

    [Export]
    public string ConfirmButtonText
    {
        get => _confirmButtonText;
        set
        {
            _confirmButtonText = value;
            RefreshContents();
        }
    }

    [Export]
    public string CancelButtonText
    {
        get => _cancelButtonText;
        set
        {
            _cancelButtonText = value;
            RefreshContents();
        }
    }

    public override void _Ready()
    {
        _messageLabel = GetNode<Label>("%MessageLabel");
        _confirmButton = GetNode<Button>("%ConfirmButton");
        _cancelButton = GetNode<Button>("%CancelButton");

        _confirmButton.Pressed += Confirm;
        _cancelButton.Pressed += Cancel;
        CloseRequested += Cancel;
        VisibilityChanged += OnVisibilityChanged;

        RefreshContents();
    }

    public override void _Input(InputEvent @event)
    {
        if (
            !Visible
            || @event is not InputEventKey keyEvent
            || !keyEvent.Pressed
            || keyEvent.Echo
        )
        {
            return;
        }

        Key key = keyEvent.PhysicalKeycode != Key.None
            ? keyEvent.PhysicalKeycode
            : keyEvent.Keycode;
        switch (key)
        {
            case Key.Escape:
                Cancel();
                GetViewport().SetInputAsHandled();
                break;
            case Key.Enter:
            case Key.KpEnter:
                Confirm();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _ExitTree()
    {
        if (_confirmButton is not null)
            _confirmButton.Pressed -= Confirm;

        if (_cancelButton is not null)
            _cancelButton.Pressed -= Cancel;

        CloseRequested -= Cancel;
        VisibilityChanged -= OnVisibilityChanged;
        _messageLabel = null;
        _confirmButton = null;
        _cancelButton = null;
    }

    private void Confirm()
    {
        Hide();
        EmitSignal(SignalName.Confirmed);
    }

    private void Cancel()
    {
        Hide();
        EmitSignal(SignalName.Canceled);
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
            _confirmButton?.GrabFocus();
    }

    private void RefreshContents()
    {
        if (_messageLabel is not null)
            _messageLabel.Text = _dialogText;

        if (_confirmButton is not null)
            _confirmButton.Text = _confirmButtonText;

        if (_cancelButton is not null)
            _cancelButton.Text = _cancelButtonText;
    }
}
