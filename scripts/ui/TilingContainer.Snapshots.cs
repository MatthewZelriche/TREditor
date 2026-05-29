using System;
using System.Collections.Generic;
using Godot;

public partial class TilingContainer
{
    public LayoutSnapshotNode CaptureLayout(Func<Control, string> getPaneId)
    {
        if (_root == null || getPaneId == null)
        {
            return null;
        }

        return CaptureNode(_root, getPaneId);
    }

    public bool RestoreLayout(
        LayoutSnapshotNode snapshot,
        Func<string, Control> paneFactory,
        bool queueFreeReplacedPanes = false
    )
    {
        if (snapshot == null || paneFactory == null)
        {
            return false;
        }

        List<Control> panes = [];
        LayoutNode newRoot = CreateNodeFromSnapshot(snapshot, paneFactory, panes);
        if (newRoot == null || panes.Count == 0 || HasDuplicatePanes(panes))
        {
            return false;
        }

        foreach (Control pane in panes)
        {
            if (!CanManageChild(pane))
            {
                return false;
            }

            Node parent = pane.GetParent();
            if (parent != null && parent != this)
            {
                return false;
            }
        }

        DestroySplitHandles(_root);
        DetachAllManagedPanes(queueFreeReplacedPanes);

        _root = newRoot;

        foreach (Control pane in panes)
        {
            if (!TryAttachChild(pane))
            {
                DestroySplitHandles(_root);
                DetachAllManagedPanes(false);
                _root = null;
                InvalidateLayout();
                return false;
            }
        }

        InvalidateLayout();
        return true;
    }

    private LayoutSnapshotNode CaptureNode(LayoutNode node, Func<Control, string> getPaneId)
    {
        if (node is LeafNode leaf)
        {
            return new LayoutSnapshotNode { Type = "pane", PaneId = getPaneId(leaf.Child) };
        }

        SplitNode split = (SplitNode)node;
        return new LayoutSnapshotNode
        {
            Type = "split",
            Axis = split.Axis.ToString(),
            Ratio = split.Ratio,
            First = CaptureNode(split.First, getPaneId),
            Second = CaptureNode(split.Second, getPaneId),
        };
    }

    private LayoutNode CreateNodeFromSnapshot(
        LayoutSnapshotNode snapshot,
        Func<string, Control> paneFactory,
        List<Control> panes
    )
    {
        if (snapshot == null)
        {
            return null;
        }

        switch (snapshot.Type)
        {
            case "pane":
                if (string.IsNullOrEmpty(snapshot.PaneId))
                {
                    return null;
                }

                Control pane = paneFactory(snapshot.PaneId);
                if (!CanManageChild(pane))
                {
                    return null;
                }

                panes.Add(pane);
                return new LeafNode(pane);

            case "split":
                if (!Enum.TryParse(snapshot.Axis, out SplitAxis axis))
                {
                    return null;
                }

                LayoutNode first = CreateNodeFromSnapshot(snapshot.First, paneFactory, panes);
                LayoutNode second = CreateNodeFromSnapshot(snapshot.Second, paneFactory, panes);

                if (first == null || second == null)
                {
                    return null;
                }

                return new SplitNode(axis, first, second)
                {
                    Ratio = Mathf.Clamp(snapshot.Ratio, 0.0f, 1.0f),
                };
        }

        return null;
    }

    private static bool HasDuplicatePanes(List<Control> panes)
    {
        HashSet<Control> seen = [];
        foreach (Control pane in panes)
        {
            if (!seen.Add(pane))
            {
                return true;
            }
        }

        return false;
    }
}
