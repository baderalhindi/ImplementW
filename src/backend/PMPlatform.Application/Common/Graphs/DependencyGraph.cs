namespace PMPlatform.Application.Common.Graphs;

/// <summary>
/// A dependency network as a directed graph of record ids, acyclic by rule: a dependency that would close a cycle is refused
/// before it is saved, and the nodes can be ordered so every predecessor comes before its successors. Domain-neutral (M-10):
/// WF-03's schedule activities (VAL-SCH-008) and WF-04's tasks (TASK-048) use it.
/// </summary>
internal static class DependencyGraph
{
    /// <summary>Whether adding <paramref name="predecessor"/> → <paramref name="successor"/> to <paramref name="edges"/> closes a cycle.</summary>
    public static bool ClosesCycle(IEnumerable<(Guid Predecessor, Guid Successor)> edges, Guid predecessor, Guid successor)
    {
        ArgumentNullException.ThrowIfNull(edges);
        if (predecessor == successor)
        {
            return true;
        }

        // The new edge closes a cycle exactly when its successor already reaches its predecessor.
        ILookup<Guid, Guid> next = edges.ToLookup(e => e.Predecessor, e => e.Successor);
        HashSet<Guid> seen = [successor];
        Stack<Guid> pending = new([successor]);
        while (pending.TryPop(out Guid node))
        {
            foreach (Guid reached in next[node])
            {
                if (reached == predecessor)
                {
                    return true;
                }

                if (seen.Add(reached))
                {
                    pending.Push(reached);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <paramref name="nodes"/> ordered so each comes after all its predecessors (Kahn), ties kept in the order given.
    /// </summary>
    /// <exception cref="InvalidOperationException">The edges hold a cycle, which the save path never lets in.</exception>
    public static IReadOnlyList<Guid> TopologicalOrder(IReadOnlyList<Guid> nodes, IEnumerable<(Guid Predecessor, Guid Successor)> edges)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        HashSet<Guid> known = [.. nodes];
        List<(Guid Predecessor, Guid Successor)> inside = [.. edges.Where(e => known.Contains(e.Predecessor) && known.Contains(e.Successor))];
        Dictionary<Guid, int> incoming = nodes.ToDictionary(n => n, _ => 0);
        foreach ((_, Guid successor) in inside)
        {
            incoming[successor]++;
        }

        ILookup<Guid, Guid> next = inside.ToLookup(e => e.Predecessor, e => e.Successor);
        Queue<Guid> ready = new(nodes.Where(n => incoming[n] == 0));
        List<Guid> order = new(nodes.Count);
        while (ready.TryDequeue(out Guid node))
        {
            order.Add(node);
            foreach (Guid successor in next[node])
            {
                if (--incoming[successor] == 0)
                {
                    ready.Enqueue(successor);
                }
            }
        }

        return order.Count == nodes.Count ? order : throw new InvalidOperationException("The dependency network holds a cycle.");
    }
}
