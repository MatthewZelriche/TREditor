using System;
using Godot;

public partial class LengthToolSettings : VBoxContainer
{
    private const string ScenePath = "res://src/ui/toolsettings/LengthToolSettings.tscn";

    private LengthToolBase _tool = null!;
    private SpinBox _lengthSpin = null!;
    private bool _syncing;

    public static LengthToolSettings Create(
        LengthToolBase tool,
        string settingsName,
        string settingsTitle
    )
    {
        ArgumentNullException.ThrowIfNull(tool);

        PackedScene scene =
            GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Failed to load '{ScenePath}'.");
        LengthToolSettings control = scene.Instantiate<LengthToolSettings>();
        control.Bind(tool, settingsName, settingsTitle);
        return control;
    }

    private void Bind(LengthToolBase tool, string settingsName, string settingsTitle)
    {
        _tool = tool;
        Name = settingsName;
        GetNode<Label>("%Title").Text = settingsTitle;
        _lengthSpin = GetNode<SpinBox>("%LengthSpin");
        _lengthSpin.MinValue = LengthToolBase.MinimumLength;
        _lengthSpin.Step = LengthToolBase.MinimumLength;
        _tool.PrimaryActionChanged += Sync;
        Sync();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && _tool is not null)
            _tool.PrimaryActionChanged -= Sync;
    }

    private void Sync()
    {
        _syncing = true;
        _lengthSpin.Value = _tool.Length;
        _syncing = false;
    }

    // Wired from .tscn.
    private void OnLengthChanged(double value)
    {
        if (!_syncing)
            _tool.Length = (float)value;
    }
}
