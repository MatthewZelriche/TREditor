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

    public event Action<float?>? GridSnapChanged;
    public event Action<float?>? AngleSnapChanged;
    public event Action<bool>? LocalSpaceChanged;

    public float? GridSnap { get; private set; } = 16.0f;
    public float? AngleSnap { get; private set; }
    public bool IsLocalSpace { get; private set; } = true;

    public override void _Ready()
    {
        MenuButton gridButton = GetNode<MenuButton>("BarMargin/Controls/GridSnapButton");
        MenuButton angleButton = GetNode<MenuButton>("BarMargin/Controls/AngleSnapButton");
        MenuButton spaceButton = GetNode<MenuButton>("BarMargin/Controls/SpaceButton");

        ConfigureSnapMenu(
            gridButton,
            "Grid",
            GridSnapOptions,
            GridSnap,
            value =>
            {
                GridSnap = value;
                GridSnapChanged?.Invoke(value);
            }
        );
        ConfigureSnapMenu(
            angleButton,
            "Angle",
            AngleSnapOptions,
            AngleSnap,
            value =>
            {
                AngleSnap = value;
                AngleSnapChanged?.Invoke(value);
            }
        );
        ConfigureSpaceMenu(spaceButton);
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
