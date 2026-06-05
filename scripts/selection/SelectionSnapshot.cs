using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct SelectionSnapshot : IEquatable<SelectionSnapshot>
{
    private readonly SelectionTarget[] _targets;

    private SelectionSnapshot(SelectionTarget[] targets)
    {
        _targets = targets;
    }

    public static SelectionSnapshot Empty { get; } = new([]);

    public IReadOnlyList<SelectionTarget> Targets => _targets ?? [];

    public int Count => _targets?.Length ?? 0;

    public bool IsEmpty => Count == 0;

    public static SelectionSnapshot From(IEnumerable<SelectionTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        List<SelectionTarget> uniqueTargets = [];
        foreach (SelectionTarget target in targets)
        {
            if (!uniqueTargets.Contains(target))
            {
                uniqueTargets.Add(target);
            }
        }

        return uniqueTargets.Count == 0 ? Empty : new SelectionSnapshot(uniqueTargets.ToArray());
    }

    public bool Contains(SelectionTarget target) => Targets.Contains(target);

    public SelectionSnapshot Replace(SelectionTarget target) => From([target]);

    public SelectionSnapshot Add(SelectionTarget target)
    {
        if (Contains(target))
        {
            return this;
        }

        return From(Targets.Append(target));
    }

    public SelectionSnapshot Remove(SelectionTarget target)
    {
        if (!Contains(target))
        {
            return this;
        }

        return From(Targets.Where(existing => existing != target));
    }

    public SelectionSnapshot Toggle(SelectionTarget target) =>
        Contains(target) ? Remove(target) : Add(target);

    public SelectionSnapshot Apply(SelectionChangeMode mode, SelectionTarget target) =>
        mode switch
        {
            SelectionChangeMode.Replace => Replace(target),
            SelectionChangeMode.Add => Add(target),
            SelectionChangeMode.Remove => Remove(target),
            SelectionChangeMode.Toggle => Toggle(target),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    public bool Equals(SelectionSnapshot other) => Targets.SequenceEqual(other.Targets);

    public override bool Equals(object obj) => obj is SelectionSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (SelectionTarget target in Targets)
        {
            hash.Add(target);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(SelectionSnapshot left, SelectionSnapshot right) =>
        left.Equals(right);

    public static bool operator !=(SelectionSnapshot left, SelectionSnapshot right) =>
        !left.Equals(right);
}
