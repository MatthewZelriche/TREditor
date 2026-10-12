using System;
using Godot;

public partial class MergeVerticesSettings : VBoxContainer
{
    private const string ScenePath = "res://src/ui/toolsettings/MergeVerticesSettings.tscn";

    private MergeVerticesTool _tool = null!;
    private OptionButton _positionOption = null!;
    private bool _syncing;

    public static MergeVerticesSettings Create(MergeVerticesTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        PackedScene scene =
            GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Failed to load '{ScenePath}'.");
        MergeVerticesSettings control = scene.Instantiate<MergeVerticesSettings>();
        control.Bind(tool);
        return control;
    }

    private void Bind(MergeVerticesTool tool)
    {
        _tool = tool;
        _positionOption = GetNode<OptionButton>("%PositionOption");
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
        _positionOption.Select((int)_tool.Position);
        _syncing = false;
    }

    // Wired from .tscn.
    private void OnPositionSelected(long index)
    {
        if (!_syncing)
            _tool.Position = (VertexMergePosition)_positionOption.GetItemId((int)index);
    }
}
