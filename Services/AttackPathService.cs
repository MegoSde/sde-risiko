using AlpacaMedic.Models;

namespace AlpacaMedic.Services;

public record AttackPath(int[] ComponentIds, int[] ConnectionIds, int PathLikelihood, int? CycleToComponentId);

public class AttackPathService
{
    // Enumerate complete directed paths. Visited nodes belong to each path, not the whole graph:
    // a shared downstream component must remain reachable through every incoming branch.
    public List<AttackPath> Find(int initialId, IReadOnlyDictionary<int, AttackComponent> components,
        IReadOnlyCollection<AttackConnection> connections, CancellationToken cancellationToken = default)
    {
        var outgoing = connections.GroupBy(c => c.FromAttackComponentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id).ToArray());
        var paths = new List<AttackPath>();
        var pending = new Stack<(int[] Nodes, int[] Edges, int Likelihood, int? CycleTo)>();
        pending.Push(([initialId], [], components[initialId].Likelihood, null));
        while (pending.TryPop(out var path))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = path.Nodes[^1];
            if (path.CycleTo.HasValue || !outgoing.TryGetValue(current, out var next) || next.Length == 0)
            {
                paths.Add(new(path.Nodes, path.Edges, path.Likelihood, path.CycleTo));
                continue;
            }
            // Reverse stack insertion keeps the resulting path order stable.
            foreach (var connection in next.Reverse())
            {
                var target = connection.ToAttackComponentId;
                var edges = path.Edges.Append(connection.Id).ToArray();
                if (path.Nodes.Contains(target))
                    pending.Push((path.Nodes, edges, path.Likelihood, target));
                else
                    pending.Push((path.Nodes.Append(target).ToArray(), edges,
                        Math.Min(path.Likelihood, components[target].Likelihood), null));
            }
        }
        return paths;
    }
}
