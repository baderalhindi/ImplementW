// The backend's `DependencyGraph` (Application/Common/Graphs), shared by the schedule's activity network (WF-03) and
// the tasks' network (WF-04), mirrored so a dependency that would close a cycle is refused before anything is sent.

export interface GraphEdge {
  from: string;
  to: string;
}

/**
 * The chain a new predecessor → successor edge would close into a cycle: the nodes from the successor along existing
 * edges to the predecessor, in order. Null when it closes none. The same node at both ends is a cycle of one.
 */
export function cyclePath(
  edges: GraphEdge[],
  predecessor: string,
  successor: string,
): string[] | null {
  if (predecessor === successor) {
    return [successor];
  }
  const next = new Map<string, string[]>();
  for (const edge of edges) {
    next.set(edge.from, [...(next.get(edge.from) ?? []), edge.to]);
  }
  // Breadth first from the successor, so the chain shown is the shortest one.
  const cameFrom = new Map<string, string | null>([[successor, null]]);
  const queue = [successor];
  for (let node = queue.shift(); node !== undefined; node = queue.shift()) {
    if (node === predecessor) {
      const path: string[] = [];
      for (let at: string | null = node; at !== null; at = cameFrom.get(at) ?? null) {
        path.unshift(at);
      }
      return path;
    }
    for (const reached of next.get(node) ?? []) {
      if (!cameFrom.has(reached)) {
        cameFrom.set(reached, node);
        queue.push(reached);
      }
    }
  }
  return null;
}
