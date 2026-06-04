using Godot;

public partial class StatusBar : PanelContainer
{
    private readonly SnapOption[] _snapOptions =
    {
        new("Off", GridSnap.Off),
        new("0.125", 0.125f),
        new("0.25", 0.25f),
        new("0.5", 0.5f),
        new("1", 1.0f),
        new("2", 2.0f),
        new("5", 5.0f),
        new("10", 10.0f),
    };

    private EditorSession _session;
    private OptionButton _snapOption;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _snapOption = GetNode<OptionButton>("HBoxContainer/SnapOption");

        PopulateSnapOptions();

        if (_session == null)
        {
            GD.PushWarning("StatusBar could not find EditorSession.");
            return;
        }

        _snapOption.ItemSelected += OnSnapOptionSelected;
        OnSnapOptionSelected(_snapOption.Selected);
    }

    private void PopulateSnapOptions()
    {
        _snapOption.Clear();

        for (int i = 0; i < _snapOptions.Length; i++)
        {
            _snapOption.AddItem(_snapOptions[i].Label, i);
        }

        _snapOption.Select(0);
    }

    private void OnSnapOptionSelected(long index)
    {
        if (_session == null || index < 0 || index >= _snapOptions.Length)
        {
            return;
        }

        _session.GridSnapSize = _snapOptions[index].CellSize;
    }

    private readonly struct SnapOption
    {
        public SnapOption(string label, float cellSize)
        {
            Label = label;
            CellSize = cellSize;
        }

        public string Label { get; }
        public float CellSize { get; }
    }
}
