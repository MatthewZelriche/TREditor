#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public partial class AppMenuBar : CenterContainer
{
    public enum AppMenuAction
    {
        NewDocument = 100,
        OpenDocument = 101,
        SaveDocument = 102,
        SaveDocumentAs = 103,
        Quit = 104,

        Undo = 200,
        Redo = 201,

        Keybindings = 300,
        Preferences = 301,

        OpenSourceLicenses = 400,
        About = 401,
    }

    [Signal]
    public delegate void MenuActionRequestedEventHandler(int actionId);

    private readonly Dictionary<AppMenuAction, (PopupMenu Menu, int Index)> _menuItems = [];

    public override void _Ready()
    {
        IndexMenuItems();
        ConfigureMenuAccelerators();
    }

    public override void _ExitTree() => _menuItems.Clear();

    public void SetMenuActionDisabled(AppMenuAction action, bool disabled)
    {
        if (_menuItems.TryGetValue(action, out var item))
            item.Menu.SetItemDisabled(item.Index, disabled);
    }

    private void IndexMenuItems()
    {
        _menuItems.Clear();

        IndexMenuItems(GetNode<PopupMenu>("%FileMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%EditMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%SettingsMenu"));
        IndexMenuItems(GetNode<PopupMenu>("%AboutMenu"));
    }

    private void IndexMenuItems(PopupMenu menu)
    {
        for (int index = 0; index < menu.ItemCount; index++)
        {
            int id = menu.GetItemId(index);
            if (Enum.IsDefined(typeof(AppMenuAction), id))
                _menuItems.Add((AppMenuAction)id, (menu, index));
        }
    }

    private void ConfigureMenuAccelerators()
    {
        Key control = (Key)KeyModifierMask.MaskCtrl;
        Key controlShift = (Key)(KeyModifierMask.MaskCtrl | KeyModifierMask.MaskShift);

        SetMenuActionAccelerator(AppMenuAction.NewDocument, control | Key.N);
        SetMenuActionAccelerator(AppMenuAction.OpenDocument, control | Key.O);
        SetMenuActionAccelerator(AppMenuAction.SaveDocument, control | Key.S);
        SetMenuActionAccelerator(AppMenuAction.SaveDocumentAs, controlShift | Key.S);
        SetMenuActionAccelerator(AppMenuAction.Undo, control | Key.Z);
        SetMenuActionAccelerator(AppMenuAction.Redo, control | Key.Y);
    }

    private void SetMenuActionAccelerator(AppMenuAction action, Key accelerator)
    {
        if (_menuItems.TryGetValue(action, out var item))
            item.Menu.SetItemAccelerator(item.Index, accelerator);
    }

    // Wired from .tscn.
    private void OnMenuIdPressed(long id)
    {
        if (Enum.IsDefined(typeof(AppMenuAction), (int)id))
            EmitSignal(SignalName.MenuActionRequested, (int)id);
    }
}
