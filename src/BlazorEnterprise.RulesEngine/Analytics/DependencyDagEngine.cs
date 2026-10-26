using BlazorEnterprise.Shared.Contracts.Engine.Analytics;

namespace BlazorEnterprise.RulesEngine.Analytics;

/// <summary>
/// Directed Acyclic Graph (DAG) cycle detection and critical path algorithm engine.
/// Uses Tarjan's Strongly Connected Components (SCC) algorithm to identify circular dependencies.
/// </summary>
public static class DependencyDagEngine
{
    public static DependencyDagRespDto Analyze(DependencyDagReqDto request)
    {
        var nodeSet = new HashSet<Guid>(request.NodeIds);
        foreach (var edge in request.Edges)
        {
            nodeSet.Add(edge.SourceId);
            nodeSet.Add(edge.TargetId);
        }

        var adj = new Dictionary<Guid, List<Guid>>();
        foreach (var node in nodeSet)
        {
            adj[node] = new List<Guid>();
        }

        foreach (var edge in request.Edges)
        {
            adj[edge.SourceId].Add(edge.TargetId);
        }

        // Tarjan's SCC for Cycle Detection
        var index = 0;
        var indices = new Dictionary<Guid, int>();
        var lowlink = new Dictionary<Guid, int>();
        var onStack = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        var cycles = new List<IReadOnlyList<Guid>>();

        void StrongConnect(Guid v)
        {
            indices[v] = index;
            lowlink[v] = index;
            index++;
            stack.Push(v);
            onStack.Add(v);

            foreach (var w in adj[v])
            {
                if (!indices.ContainsKey(w))
                {
                    StrongConnect(w);
                    lowlink[v] = Math.Min(lowlink[v], lowlink[w]);
                }
                else if (onStack.Contains(w))
                {
                    lowlink[v] = Math.Min(lowlink[v], indices[w]);
                }
            }

            if (lowlink[v] == indices[v])
            {
                var component = new List<Guid>();
                Guid w;
                do
                {
                    w = stack.Pop();
                    onStack.Remove(w);
                    component.Add(w);
                } while (w != v);

                // If component contains > 1 node, or has a self-loop, it is a cycle
                if (component.Count > 1 || adj[v].Contains(v))
                {
                    cycles.Add(component);
                }
            }
        }

        foreach (var node in nodeSet)
        {
            if (!indices.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        var hasCycle = cycles.Count > 0;
        var criticalPath = new List<Guid>();

        // If acyclic, compute topological order
        if (!hasCycle)
        {
            var inDegree = nodeSet.ToDictionary(n => n, _ => 0);
            foreach (var edge in request.Edges)
            {
                inDegree[edge.TargetId]++;
            }

            var queue = new Queue<Guid>(nodeSet.Where(n => inDegree[n] == 0));
            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                criticalPath.Add(curr);

                foreach (var neighbor in adj[curr])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return new DependencyDagRespDto(
            ProjectId: request.ProjectId,
            HasCycle: hasCycle,
            Cycles: cycles,
            CriticalPath: criticalPath
        );
    }
}
