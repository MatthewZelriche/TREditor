#nullable enable

using System;
using Godot;

public partial class ViewportBottomBar : PanelContainer
{
    private static readonly (string Label, float? Value)[] GridSnapOptions =
    [
        ("Off", null),
        ("1", 1.0f),
        ("2", 2.0f),
        ("4", 4.0f),
        ("8", 8.0f),
        ("16", 16.0f),
        ("32", 32.0f),
        ("64", 64.0f),
    ];

    private static readonly (string Label, float? Value)[] AngleSnapOptions =
    [
        ("Off", null),
        ("5°", 5.0f),
        ("10°", 10.0f),
        ("15°", 15.0f),
        ("30°", 30.0f),
        ("45°", 45.0f),
        ("90°", 90.0f),
    ];

    private SnapSettings? _snap;
    private MenuButton _gridButton = null!;
    private MenuButton _angleButton = null!;
    private MenuButton _spaceButton = null!;
    private bool _menusConfigured;

    public event Action<bool>? LocalSpaceChanged;

    public bool IsLocalSpace { get; private set; } = true;

    public override void _Ready()
    {
        _gridButton = GetNode<MenuButton>("BarMargin/Controls/GridSnapButton");
        _angleButton = GetNode<MenuButton>("BarMargin/Controls/AngleSnapButton");
        _spaceButton = GetNode<MenuButton>("BarMargin/Controls/SpaceButton");
        ConfigureSpaceMenu(_spaceButton);
        TryConfigureSnapMenus();
    }

    public override void _ExitTree() => Unbind();

    public void Bind(SnapSettings snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        Unbind();
        _snap = snap;
        _snap.Changed += SyncSnapMenus;
        TryConfigureSnapMenus();
        SyncSnapMenus();
    }

    private void Unbind()
    {
        if (_snap is null)
            return;

        _snap.Changed -= SyncSnapMenus;
        _snap = null;
    }

    private void TryConfigureSnapMenus()
    {
        if (_menusConfigured || _gridButton is null || _angleButton is null || _snap is null)
            return;

        ConfigureSnapMenu(
            _gridButton,
            "Grid",
            GridSnapOptions,
            _snap.GridSnap,
            value => _snap?.SetGridSnap(value)
        );
        ConfigureSnapMenu(
            _angleButton,
            "Angle",
            AngleSnapOptions,
            _snap.AngleSnap,
            value => _snap?.SetAngleSnap(value)
        );
        _menusConfigured = true;
    }

    private void SyncSnapMenus()
    {
        if (_snap is null || !_menusConfigured)
            return;

        SetSnapSelection(_gridButton, "Grid", GridSnapOptions, _snap.GridSnap);
        SetSnapSelection(_angleButton, "Angle", AngleSnapOptions, _snap.AngleSnap);
    }

    private static void ConfigureSnapMenu(
        MenuButton button,
        string prefix,
        (string Label, float? Value)[] options,
        float? initialValue,
        Action<float?> onSelected
    )
    {
        PopupMenu popup = button.GetPopup();
        popup.Clear();

        for (int index = 0; index < options.Length; index++)
        {
            popup.AddRadioCheckItem(options[index].Label, index);
        }

        SetSnapSelection(button, prefix, options, initialValue);
        popup.IdPressed += id =>
        {
            int selectedIndex = (int)id;
            if (selectedIndex < 0 || selectedIndex >= options.Length)
            {
                return;
            }

            float? selectedValue = options[selectedIndex].Value;
            SetSnapSelection(button, prefix, options, selectedValue);
            onSelected(selectedValue);
        };
    }

    private static void SetSnapSelection(
        MenuButton button,
        string prefix,
        (string Label, float? Value)[] options,
        float? selectedValue
    )
    {
        PopupMenu popup = button.GetPopup();
        for (int index = 0; index < options.Length; index++)
        {
            popup.SetItemChecked(index, options[index].Value == selectedValue);
        }

        int selectedIndex = Array.FindIndex(options, option => option.Value == selectedValue);
        string selectedLabel = selectedIndex >= 0 ? options[selectedIndex].Label : "Off";
        button.Text = $"{prefix} {selectedLabel}";
    }

    private void ConfigureSpaceMenu(MenuButton button)
    {
        PopupMenu popup = button.GetPopup();
        popup.Clear();
        popup.AddRadioCheckItem("Local", 0);
        popup.AddRadioCheckItem("Global", 1);
        SetSpaceSelection(button, IsLocalSpace);

        popup.IdPressed += id =>
        {
            if (id is not 0 and not 1)
            {
                return;
            }

            IsLocalSpace = id == 0;
            SetSpaceSelection(button, IsLocalSpace);
            LocalSpaceChanged?.Invoke(IsLocalSpace);
        };
    }

    private static void SetSpaceSelection(MenuButton button, bool isLocal)
    {
        PopupMenu popup = button.GetPopup();
        popup.SetItemChecked(0, isLocal);
        popup.SetItemChecked(1, !isLocal);
        button.Text = isLocal ? "Local" : "Global";
    }
}
