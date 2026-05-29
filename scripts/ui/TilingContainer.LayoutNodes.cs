using System.Collections.Generic;
using Godot;

public partial class TilingContainer
{
    private abstract class LayoutNode
    {
        public SplitNode Parent;
        public Rect2 Rect;

        public abstract Vector2 GetMinimumSize(TilingContainer owner);
        public abstract LeafNode FindLeaf(Control child);
        public abstract void Layout(TilingContainer owner, Rect2 rect);
        public abstract void CollectPanes(Godot.Collections.Array<Control> panes);
        public abstract void CollectLeaves(List<LeafNode> leaves);
        public abstract void CollectSplits(List<SplitNode> splits);
    }

    private sealed class LeafNode(Control child) : LayoutNode
    {
        public Control Child = child;

        public override Vector2 GetMinimumSize(TilingContainer owner)
        {
            if (!GodotObject.IsInstanceValid(Child))
            {
                return new Vector2(owner.MinimumPaneSize, owner.MinimumPaneSize);
            }

            return owner.GetPaneMinimumSize(Child);
        }

        public override LeafNode FindLeaf(Control child)
        {
            return Child == child ? this : null;
        }

        public override void Layout(TilingContainer owner, Rect2 rect)
        {
            Rect = rect;

            if (GodotObject.IsInstanceValid(Child) && Child.GetParent() == owner)
            {
                owner.FitChildInRect(Child, rect);
            }
        }

        public override void CollectPanes(Godot.Collections.Array<Control> panes)
        {
            if (GodotObject.IsInstanceValid(Child))
            {
                panes.Add(Child);
            }
        }

        public override void CollectLeaves(List<LeafNode> leaves)
        {
            leaves.Add(this);
        }

        public override void CollectSplits(List<SplitNode> splits) { }
    }

    private sealed class SplitNode : LayoutNode
    {
        public readonly SplitAxis Axis;
        public float Ratio = DefaultSplitRatio;
        public LayoutNode First;
        public LayoutNode Second;
        public Control Handle;
        public Rect2 BorderRect;
        public Rect2 HandleRect;

        public SplitNode(SplitAxis axis, LayoutNode first, LayoutNode second)
        {
            Axis = axis;
            First = first;
            Second = second;
            First.Parent = this;
            Second.Parent = this;
        }

        public override Vector2 GetMinimumSize(TilingContainer owner)
        {
            Vector2 firstMinimum = First.GetMinimumSize(owner);
            Vector2 secondMinimum = Second.GetMinimumSize(owner);
            float borderThickness = owner.GetVisibleBorderThickness();

            if (Axis == SplitAxis.Horizontal)
            {
                return new Vector2(
                    firstMinimum.X + secondMinimum.X + borderThickness,
                    Mathf.Max(firstMinimum.Y, secondMinimum.Y)
                );
            }

            return new Vector2(
                Mathf.Max(firstMinimum.X, secondMinimum.X),
                firstMinimum.Y + secondMinimum.Y + borderThickness
            );
        }

        public override LeafNode FindLeaf(Control child)
        {
            return First.FindLeaf(child) ?? Second.FindLeaf(child);
        }

        public override void Layout(TilingContainer owner, Rect2 rect)
        {
            Rect = rect;

            float borderThickness = owner.GetVisibleBorderThickness();

            if (Axis == SplitAxis.Horizontal)
            {
                float contentSpan = Mathf.Max(0.0f, rect.Size.X - borderThickness);
                float firstSpan = owner.ClampFirstSpan(this, contentSpan, contentSpan * Ratio);
                float secondSpan = Mathf.Max(0.0f, contentSpan - firstSpan);

                Rect2 firstRect = new(rect.Position, new Vector2(firstSpan, rect.Size.Y));
                BorderRect = new Rect2(
                    rect.Position.X + firstSpan,
                    rect.Position.Y,
                    borderThickness,
                    rect.Size.Y
                );
                Rect2 secondRect = new(
                    rect.Position.X + firstSpan + borderThickness,
                    rect.Position.Y,
                    secondSpan,
                    rect.Size.Y
                );

                First.Layout(owner, firstRect);
                Second.Layout(owner, secondRect);
            }
            else
            {
                float contentSpan = Mathf.Max(0.0f, rect.Size.Y - borderThickness);
                float firstSpan = owner.ClampFirstSpan(this, contentSpan, contentSpan * Ratio);
                float secondSpan = Mathf.Max(0.0f, contentSpan - firstSpan);

                Rect2 firstRect = new(rect.Position, new Vector2(rect.Size.X, firstSpan));
                BorderRect = new Rect2(
                    rect.Position.X,
                    rect.Position.Y + firstSpan,
                    rect.Size.X,
                    borderThickness
                );
                Rect2 secondRect = new(
                    rect.Position.X,
                    rect.Position.Y + firstSpan + borderThickness,
                    rect.Size.X,
                    secondSpan
                );

                First.Layout(owner, firstRect);
                Second.Layout(owner, secondRect);
            }

            HandleRect = owner.GetHandleRect(this);
        }

        public override void CollectPanes(Godot.Collections.Array<Control> panes)
        {
            First.CollectPanes(panes);
            Second.CollectPanes(panes);
        }

        public override void CollectLeaves(List<LeafNode> leaves)
        {
            First.CollectLeaves(leaves);
            Second.CollectLeaves(leaves);
        }

        public override void CollectSplits(List<SplitNode> splits)
        {
            splits.Add(this);
            First.CollectSplits(splits);
            Second.CollectSplits(splits);
        }
    }
}
