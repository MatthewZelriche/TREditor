using System;
using Godot;

public partial class TilingContainer
{
    // Perform some sanity checking to ensure the child is valid.
    private bool CanManageChild(Control child)
    {
        return child != null
            && child != this
            && GodotObject.IsInstanceValid(child)
            && !IsHandle(child);
    }

    // Attempt to attach a child to the TilingContainer. Fails if the child is already parented to
    // another node.
    private bool TryAttachChild(Control child)
    {
        Node parent = child.GetParent();
        if (parent == this)
        {
            return true;
        }

        if (parent != null)
        {
            return false;
        }

        RunWithChildOrderPruneSuppressed(() => AddChild(child));
        return true;
    }

    private void DetachManagedChild(Control child, bool queueFreeChild)
    {
        if (queueFreeChild)
        {
            child.QueueFree();
            return;
        }

        if (child.GetParent() == this)
        {
            RunWithChildOrderPruneSuppressed(() => RemoveChild(child));
        }
    }

    // Detatch all child panes from this container. Optionally queue free the children.
    private void DetachAllManagedPanes(bool queueFreeChildren)
    {
        if (_root == null)
        {
            return;
        }

        _scratchLeaves.Clear();
        _root.CollectLeaves(_scratchLeaves);

        RunWithChildOrderPruneSuppressed(() =>
        {
            foreach (LeafNode leaf in _scratchLeaves)
            {
                Control child = leaf.Child;
                if (!GodotObject.IsInstanceValid(child) || child.GetParent() != this)
                {
                    continue;
                }

                if (queueFreeChildren)
                {
                    child.QueueFree();
                }
                else
                {
                    RemoveChild(child);
                }
            }
        });
    }

    // If we don't have a root, adopt the first non-handle child. This is so that
    // you can set it up in a scene without having to manually add a child via code.
    private void AdoptSingleExistingChildIfNeeded()
    {
        if (_root != null)
        {
            return;
        }

        Control firstPane = null;
        int paneCount = 0;

        foreach (Node child in GetChildren())
        {
            if (child is not Control control || IsHandle(control))
            {
                continue;
            }

            firstPane = control;
            paneCount++;
            if (paneCount > 1)
            {
                return;
            }
        }

        if (firstPane != null)
        {
            SetRoot(firstPane);
        }
    }

    private void ReplaceNode(LayoutNode oldNode, SplitNode oldParent, LayoutNode newNode)
    {
        newNode.Parent = oldParent;

        if (oldParent == null)
        {
            _root = newNode;
            return;
        }

        if (oldParent.First == oldNode)
        {
            oldParent.First = newNode;
        }
        else
        {
            oldParent.Second = newNode;
        }
    }

    private void CollapseLeaf(LeafNode leaf)
    {
        SplitNode parent = leaf.Parent;
        if (parent == null)
        {
            _root = null;
            return;
        }

        LayoutNode sibling = parent.First == leaf ? parent.Second : parent.First;
        SplitNode grandparent = parent.Parent;

        DestroyHandle(parent);
        sibling.Parent = grandparent;

        if (grandparent == null)
        {
            _root = sibling;
        }
        else if (grandparent.First == parent)
        {
            grandparent.First = sibling;
        }
        else
        {
            grandparent.Second = sibling;
        }
    }

    private bool PruneDetachedPanes()
    {
        if (_root == null)
        {
            return false;
        }

        _scratchLeaves.Clear();
        _root.CollectLeaves(_scratchLeaves);

        bool changed = false;
        foreach (LeafNode leaf in _scratchLeaves)
        {
            Control child = leaf.Child;
            if (!GodotObject.IsInstanceValid(child) || child.GetParent() != this)
            {
                CollapseLeaf(leaf);
                changed = true;
            }
        }

        return changed;
    }

    private void RunWithChildOrderPruneSuppressed(Action action)
    {
        bool wasSuppressingChildOrderPrune = _suppressChildOrderPrune;
        _suppressChildOrderPrune = true;

        try
        {
            action();
        }
        finally
        {
            _suppressChildOrderPrune = wasSuppressingChildOrderPrune;
        }
    }
}
