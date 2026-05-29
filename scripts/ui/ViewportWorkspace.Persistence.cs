using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

public partial class ViewportWorkspace
{
    private const string LayoutPath = "user://viewport_layout.json";
    private const int LayoutVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private void SaveLayout()
    {
        LayoutSnapshotNode snapshot = CaptureLayout(control =>
            control is ViewportPane pane ? pane.PaneId : ""
        );
        if (snapshot == null)
        {
            return;
        }

        PersistedLayout layout = new()
        {
            Version = LayoutVersion,
            NextPaneNumber = _nextPaneNumber,
            Root = snapshot,
            Panes = CapturePersistedPanes(),
        };

        try
        {
            using FileAccess file = FileAccess.Open(LayoutPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushWarning($"Unable to save viewport layout: {FileAccess.GetOpenError()}");
                return;
            }

            file.StoreString(JsonSerializer.Serialize(layout, JsonOptions));
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to save viewport layout: {exception.Message}");
        }
    }

    private bool RestoreSavedLayout()
    {
        if (!FileAccess.FileExists(LayoutPath))
        {
            return false;
        }

        PersistedLayout layout;
        try
        {
            using FileAccess file = FileAccess.Open(LayoutPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushWarning($"Unable to read viewport layout: {FileAccess.GetOpenError()}");
                return false;
            }

            layout = JsonSerializer.Deserialize<PersistedLayout>(file.GetAsText(), JsonOptions);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Unable to read viewport layout: {exception.Message}");
            return false;
        }

        if (layout == null)
        {
            return false;
        }

        if (layout.Version != LayoutVersion)
        {
            GD.PushWarning(
                $"Unsupported viewport layout version {layout.Version}; expected {LayoutVersion}."
            );
            return false;
        }

        if (layout.Root == null || layout.Panes == null || layout.Panes.Count == 0)
        {
            return false;
        }

        Dictionary<string, PersistedPane> persistedPanes = [];
        foreach (PersistedPane pane in layout.Panes)
        {
            if (!string.IsNullOrEmpty(pane.Id))
            {
                persistedPanes[pane.Id] = pane;
            }
        }

        Dictionary<string, ViewportPane> restoredPanes = [];
        bool restored = RestoreLayout(
            layout.Root,
            paneId =>
            {
                if (
                    restoredPanes.ContainsKey(paneId)
                    || !persistedPanes.TryGetValue(paneId, out PersistedPane persistedPane)
                )
                {
                    return null;
                }

                ViewportPane pane = CreatePane(
                    persistedPane.Id,
                    persistedPane.Title,
                    persistedPane.ToColor()
                );
                restoredPanes[paneId] = pane;
                return pane;
            },
            true
        );

        if (!restored)
        {
            foreach (ViewportPane pane in restoredPanes.Values)
            {
                if (GodotObject.IsInstanceValid(pane))
                {
                    pane.QueueFree();
                }
            }

            return false;
        }

        _nextPaneNumber = Math.Max(layout.NextPaneNumber, GetMinimumNextPaneNumber());
        return true;
    }

    private List<PersistedPane> CapturePersistedPanes()
    {
        List<PersistedPane> panes = [];
        foreach (ViewportPane pane in GetViewportPanes())
        {
            panes.Add(
                new PersistedPane
                {
                    Id = pane.PaneId,
                    Title = pane.Title,
                    Red = pane.PaneColor.R,
                    Green = pane.PaneColor.G,
                    Blue = pane.PaneColor.B,
                    Alpha = pane.PaneColor.A,
                }
            );
        }

        return panes;
    }

    private sealed class PersistedLayout
    {
        public int Version { get; set; } = LayoutVersion;
        public int NextPaneNumber { get; set; } = 1;
        public LayoutSnapshotNode Root { get; set; }
        public List<PersistedPane> Panes { get; set; } = [];
    }

    private sealed class PersistedPane
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public float Red { get; set; } = 0.12f;
        public float Green { get; set; } = 0.14f;
        public float Blue { get; set; } = 0.17f;
        public float Alpha { get; set; } = 1.0f;

        public Color ToColor()
        {
            return new Color(Red, Green, Blue, Alpha);
        }
    }
}
